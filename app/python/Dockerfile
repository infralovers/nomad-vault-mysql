FROM python:3.12-slim

WORKDIR /usr/src/app

RUN pip install --no-cache-dir uv

COPY pyproject.toml ./pyproject.toml
RUN uv sync --no-dev

COPY templates ./templates/
COPY static ./static/
COPY app.py db_client.py db_client_transform.py ./
COPY config/config.ini ./config/config.ini

CMD ["uv", "run", "app.py"]
