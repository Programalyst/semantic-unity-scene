import asyncio
import websockets
import logging
import os
import json

logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')

async def connect_to_unity_mpe(unity_port):
    """This is the persistent data channel."""
    uri = f"ws://127.0.0.1:{unity_port}/sus-agent-channel"
    try:
        async with websockets.connect(uri) as websocket:
            logging.info(f"Connected to Unity MPE on port {unity_port}")
            
            async for message in websocket:
                try:
                     # 1. Strip Unity's 4-byte ID header
                    payload = message[4:] if isinstance(message, bytes) else message
        
                    # 2. GUARD: If it doesn't look like JSON, just ignore it silently
                    if not payload or not str(payload).strip().startswith('{'):
                        # "Catch" numeric Unity ClientID and ignore without an error
                        continue 

                    # 3. Parse only if it passed the guard
                    data = json.loads(payload)
                    
                    scene_json = data.get("sceneJson", {})
                    b64_image = data.get("b64Image", "")
                    
                    logging.info(f"SUCCESS: Received JSON ({len(str(scene_json))} chars) and Image {len(str(b64_image))} chars")
                    
                    # TODO: Trigger LLM here
                    
                except json.JSONDecodeError as je:
                    logging.warning(f"Received a malformed JSON payload. Error: {je}")
                except Exception as e:
                    logging.error(f"Error processing message: {e}")

    except Exception as e:
        logging.error(f"Failed to connect to Unity MPE: {e}")

async def handle_handshake(websocket):
    """This handles the initial 'mpe_init' from Unity."""
    try:
        async for message in websocket:
            data = json.loads(message)
            if data.get("type") == "mpe_init":
                unity_port = data.get("port")
                logging.info(f"Received Unity MPE port: {unity_port}")
                
                # Start the MPE client as a separate task so this handler can finish cleanly
                asyncio.create_task(connect_to_unity_mpe(unity_port))
                
                await websocket.send(json.dumps({"status": "ok"}))
                return # close the handshake socket
            
    except Exception as e:
        logging.error(f"Handshake error: {e}")

async def main():
    
    async with websockets.serve(handle_handshake, "127.0.0.1", 8765):
        logging.info("Python Handshake Server listening on 127.0.0.1:8765...")
        await asyncio.Future()  # This keeps the server running forever

if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        logging.info("Server shutting down...")