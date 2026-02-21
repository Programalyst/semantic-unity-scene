import logging
import json
import asyncio
from imageAnalysis import gemini_image_analysis

logger = logging.getLogger(__name__)
processing_lock = asyncio.Lock()

async def handle_unity_payload(websocket, payload_string):
    """
    Main logic hub: Parses JSON, calls Gemini, and sends tools back.
    """
    # If we are already busy thinking or sending, skip this frame
    if processing_lock.locked():
        return

    try:
        data = json.loads(payload_string)
        scene_json = data.get("sceneJson")
        b64_image = data.get("b64Image")

        if not scene_json or not b64_image:
            logger.warning("Received partial payload. Skipping Gemini analysis.")
            return

        logger.info(f"Processing Scene {len(str(scene_json))} chars + Image {len(str(b64_image))} chars")

        # 4. Call Gemini
        response = await gemini_image_analysis(scene_json, b64_image)

        # 5. Process and send Tools back to Unity
        if response:
            await handle_gemini_response(websocket, response)
        
        # 6. Let the socket clear
        await asyncio.sleep(0.1) 

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
            
            # ---- For debugging ----
            for call in calls:
                name = call.get('name')
                args = call.get('args', {})
                
                if name == 'click_screen_position':
                    x, y = args.get('screenX'), args.get('screenY')
                    intent = args.get('Intent', 'No intent provided')
                    logger.info(f"🎯 [TOOL] click_screen_position: ({x}, {y}) | Intent: {intent}")
                elif name == 'press_ui_button':
                    btn = args.get('ButtonName')
                    logger.info(f"🔘 [TOOL] press_ui_button: {btn}")
            # ---- ----

            payload = {
                "type": "function_call", 
                "content": calls
            }
            
        # 2. Handle Plain Text Response
        else:
            payload = {
                "type": "text",
                "content": gemini_response.text
            }
            logger.info(f"🤖 [TEXT]: {gemini_response.text}")

        # 3. Send over MPE (Unity MPE handles the 4-byte header on receipt)
        await websocket.send(json.dumps(payload))
        
    except Exception as e:
        logging.error(f"Failed to send tool call to Unity: {e}")