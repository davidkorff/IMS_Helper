from pydantic import BaseSettings
from typing import Optional

class Settings(BaseSettings):
    # API Gateway Configuration
    IMS_GATEWAY_URL: str
    IMS_GATEWAY_API_KEY: str
    
    # Database Configuration
    DATABASE_URL: str
    
    # Redis Configuration
    REDIS_URL: str
    
    # RabbitMQ Configuration
    RABBITMQ_URL: str
    
    # Service Configuration
    SERVICE_NAME: str = "ims-integration-service"
    ENVIRONMENT: str = "development"
    
    class Config:
        env_file = ".env"

settings = Settings() 