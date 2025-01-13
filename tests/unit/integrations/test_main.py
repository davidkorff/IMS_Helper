from fastapi.testclient import TestClient
from src.integrations.main import app

client = TestClient(app)

def test_health_check():
    """Test health check endpoint"""
    response = client.get("/health")
    assert response.status_code == 200
    assert response.json() == {
        "status": "healthy",
        "version": "1.0.0"
    }

def test_cors():
    """Test CORS headers"""
    response = client.options("/health")
    assert response.headers["access-control-allow-origin"] == "*" 