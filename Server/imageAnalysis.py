from google import genai
from google.genai import types
from google.genai.types import FunctionCall  # Import the type explicitly
import os
from dotenv import load_dotenv
from toolDeclarations import clickScreenPosition, clickUiButton

from pathlib import Path

import base64
import json

load_dotenv() # Load environment variables from .env file

# Configure the client and tools
gemini_sdk_client = genai.Client(api_key=os.getenv("GEMINI_API_KEY"))
tools = types.Tool(function_declarations=[clickScreenPosition, clickUiButton])
tool_config = types.ToolConfig(
    function_calling_config=types.FunctionCallingConfig(
        mode="auto" #default
    )
)

system_prompt = Path("system_prompt.txt").read_text(encoding="utf-8")

config = types.GenerateContentConfig(
    temperature=0,
    automatic_function_calling=types.AutomaticFunctionCallingConfig(disable=False),
    tools=[tools],
    tool_config=tool_config,
    system_instruction=system_prompt
)

async def gemini_image_analysis(image_text: list[dict[str, str]]) -> list[FunctionCall] | str:
    
    for chunk in image_text:
        if chunk["mime_type"] == "image/jpeg":
            b64encoded_image = chunk["data"]
            
        elif chunk["mime_type"] == "text/plain":
            prompt = chunk["data"]

    image_bytes = base64.b64decode(b64encoded_image)
    image = types.Part.from_bytes(data=image_bytes, mime_type="image/jpeg")

    response = gemini_sdk_client.models.generate_content(
        model="gemini-2.5-flash",
        contents=[prompt,image],
        config=config,
    )
    print(f"Tokens used: {response.usage_metadata}")

    return response
    
