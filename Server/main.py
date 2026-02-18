import asyncio
import websockets
import logging
import os
import json

logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')

async def connect_to_unity_mpe(unity_port):
    # This is the secondary connection back to Unity
    uri = f"ws://127.0.0.1:{unity_port}/sus-agent-channel"
    try:
        async with websockets.connect(uri) as websocket:
            logging.info(f"Successfully connected to Unity MPE on port {unity_port}!")
            await websocket.send("Hello from Semantic Unity Scene agent server.")
            
            async for message in websocket:
                logging.info(f"Unity says: {message}")
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