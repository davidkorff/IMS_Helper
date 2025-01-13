import pytest
from unittest.mock import Mock, patch
from src.integrations.base_integration import IntegrationBase

class TestIntegration(IntegrationBase):
    """Test implementation of abstract base class"""
    async def process_quote(self, data: dict) -> dict:
        return {"quote_id": "test"}
    
    async def sync_documents(self, data: dict) -> list:
        return []

@pytest.fixture
def integration():
    config = {"api_key": "test"}
    return TestIntegration(config)

@pytest.mark.asyncio
async def test_validate_data():
    """Test data validation"""
    from pydantic import BaseModel
    
    class TestData(BaseModel):
        name: str
        value: int
    
    integration = TestIntegration({"api_key": "test"})
    test_data = TestData(name="test", value=1)
    
    result = await integration.validate_data(test_data)
    assert result == True

@pytest.mark.asyncio
async def test_process_quote():
    """Test quote processing"""
    integration = TestIntegration({"api_key": "test"})
    result = await integration.process_quote({"test": "data"})
    assert result["quote_id"] == "test" 