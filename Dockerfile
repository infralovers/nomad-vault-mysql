FROM python:3

WORKDIR /usr/src/app
ADD requirements.txt /tmp/
RUN pip install --no-cache-dir -r /tmp/requirements.txt

ADD templates ./templates/
ADD static ./static/
ADD app.py db_client.py db_client_transform.py ./
ADD config/config.ini ./config/


VOLUME ["/usr/src/app/config"]

CMD python ./app.py
