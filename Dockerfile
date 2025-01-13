# Use multi-stage build
FROM python:3.11-slim as builder

WORKDIR /app

# Install poetry
RUN pip install poetry

# Copy dependency files
COPY pyproject.toml poetry.lock ./

# Install dependencies
RUN poetry export -f requirements.txt --output requirements.txt --without-hashes

FROM python:3.11-slim as runtime

WORKDIR /app

# Copy requirements from builder
COPY --from=builder /app/requirements.txt .

# Install runtime dependencies
RUN pip install --no-cache-dir -r requirements.txt

# Copy application code
COPY ./src ./src

# Run the application
CMD ["uvicorn", "src.integrations.main:app", "--host", "0.0.0.0", "--port", "8000"] 