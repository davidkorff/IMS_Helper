from fastapi import FastAPI, HTTPException
from .models import QuoteRequest, QuoteResponse
from .integration import AppliedEpicIntegration

app = FastAPI()
integration = AppliedEpicIntegration()

@app.post("/quotes", response_model=QuoteResponse)
async def create_quote(request: QuoteRequest):
    try:
        result = await integration.process_quote(request.dict())
        return QuoteResponse(**result)
    except Exception as e:
        raise HTTPException(status_code=400, detail=str(e)) 