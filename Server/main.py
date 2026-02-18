import asyncio
import websockets
import logging
import os
import json

logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')

async def connect_to_unity_mpe(unity_port):
    uri = f"ws://127.0.0.1:{unity_port}/sus-agent-channel"
    try:
        async with websockets.connect(uri) as websocket:
            logging.info(f"Connected to Unity MPE on port {unity_port}")
            
            async for message in websocket:
                try:
                    # 1. Handle Binary vs String
                    if isinstance(message, str):
                        # If it's a string, Unity might have already stripped the ID 
                        # or it's a heartbeat. Try to parse directly.
                        clean_json = message if message.startswith('{') else message[4:]
                    else:
                        # If it's bytes, strictly strip the 4-byte ClientID
                        clean_json = message[4:].decode('utf-8')

                    # 2. Parse JSON
                    data = json.loads(clean_json)
                    
                    # 3. Use the data
                    scene_json = data.get("sceneJson", {})
                    b64_image = data.get("b64Image", "")
                    
                    logging.info(f"SUCCESS: Received JSON ({len(str(scene_json))} chars) and Image.")
                    
                    # TODO: Trigger LLM here
                    
                except json.JSONDecodeError as je:
                    logging.warning(f"Skipping non-JSON message: {message[:20]}... Error: {je}")
                except Exception as e:
                    logging.error(f"Error processing message: {e}")

    except Exception as e:
        logging.error(f"Failed to connect to Unity MPE: {e}")

async def handle_handshake(websocket):
    
    async for message in websocket:
        try:
            data = json.loads(message)
            if data.get("type") == "mpe_init":
                unity_port = data.get("port")
                logging.info(f"Received Unity MPE port: {unity_port}")
                
                # Start the MPE client in the background
                asyncio.create_task(connect_to_unity_mpe(unity_port))
        except Exception as e:
            logging.error(f"Error processing handshake: {e}")

async def main():
    
    async with websockets.serve(handle_handshake, "127.0.0.1", 8765):
        logging.info("Python Handshake Server listening on 127.0.0.1:8765...")
        await asyncio.Future()  # This keeps the server running forever

if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        logging.info("Server shutting down...")