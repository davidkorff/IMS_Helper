from abc import ABC, abstractmethod
from pydantic import BaseModel

class IntegrationBase(ABC):
    """Base class for all Python-based integrations"""
    
    def __init__(self, config: dict):
        self.config = config
        self.ims_client = IMSGatewayClient()
        
    @abstractmethod
    async def process_quote(self, data: dict) -> dict:
        """Handle quote processing"""
        pass
    
    @abstractmethod
    async def sync_documents(self, data: dict) -> list:
        """Handle document synchronization"""
        pass
    
    async def validate_data(self, data: BaseModel) -> bool:
        """Common validation logic"""
        pass 