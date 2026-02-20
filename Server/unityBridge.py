import logging
import json
from imageAnalysis import gemini_image_analysis

logger = logging.getLogger(__name__)

async def handle_unity_payload(websocket, payload_string):
    """
    Main logic hub: Parses JSON, calls Gemini, and sends tools back.
    """
    try:
        data = json.loads(payload_string)
        scene_json = data.get("sceneJson")
        b64_image = data.get("b64Image")

        if not scene_json or not b64_image:
            logger.warning("Received partial payload. Skipping Gemini analysis.")
            return

        logger.info(f"Processing Scene ({len(str(scene_json))} chars) + Image ({len(str(b64_image))} chars)")

        # 4. Call Gemini
        response = await gemini_image_analysis(scene_json, b64_image)

        # 5. Process and send Tools back to Unity
        if response:
            await handle_gemini_response(websocket, response)

    except json.JSONDecodeError:
        logging.error("Failed to decode JSON from Unity.")
    except Exception as e:
        logging.error(f"Error in payload handler: {e}")

async def handle_gemini_response(websocket, gemini_response):
    """
    Parses the Gemini response and sends the command back to Unity 
    via the established MPE WebSocket.
    """
    try:
        # 1. Handle Function Calls (Tools)
        if gemini_response.function_calls:
            # Gemini SDK's function_calls can be converted to dicts
            calls = [fc.to_json_dict() for fc in gemini_response.function_calls]
            
            payload = {
                "type": "function_call",
                "content": calls
            }
            logger.info(f"Executing Tools: {[c['name'] for c in calls]}")
            
        # 2. Handle Plain Text Response
        else:
            payload = {
                "type": "text",
                "content": gemini_response.text
            }
            logger.info(f"Agent says: {gemini_response.text}")

        # 3. Send over MPE (Unity MPE handles the 4-byte header on receipt)
        await websocket.send(json.dumps(payload))
        
    except Exception as e:
        logging.error(f"Failed to send tool call to Unity: {e}")