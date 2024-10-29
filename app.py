import configparser
from datetime import datetime
from os import getenv
import json
import logging
import logging.config

from flask import Flask, request, render_template

from db_client import DbClient as TransitClient
from db_client_transform import DbClient as TransformClient

dbc: TransitClient = None

log_level = {"CRITICAL": 50, "ERROR": 40, "WARN": 30, "INFO": 20, "DEBUG": 10}

logger = logging.getLogger("app")

app = Flask(__name__)
app.config["TEMPLATES_AUTO_RELOAD"] = True


def read_config() -> configparser.ConfigParser:
    config = configparser.ConfigParser()
    with open("config/config.ini", encoding="utf-8") as f:
        config.read_file(f)
    return config


def read_vault_token():
    token = getenv("VAULT_TOKEN")
    return token.strip()


@app.route("/health", methods=["GET"])
def health():
    if not dbc.is_initialized:
        return "Unhealthy - no database", 500

    return "Healthy", 200


@app.route("/customers", methods=["GET"])
def get_customers():
    customers = dbc.get_customer_records()
    logger.debug(f"Customers: {customers}")
    return json.dumps(customers)


@app.route("/customer", methods=["GET"])
def get_customer():
    cust_no = request.args.get("cust_no")
    if not cust_no:
        return (
            "<html><body>Error: cust_no is a required argument for the customer endpoint.</body></html>",
            500,
        )
    record = dbc.get_customer_record(cust_no)
    # logger.debug('Request: {}'.format(request))
    return json.dumps(record)


@app.route("/customers", methods=["POST"])
def create_customer():
    logging.debug(f"Form Data: {dict(request.form)}")
    customer = dict(dict(request.form).items())
    for k, v in customer.items():
        if isinstance(v, list):
            customer[k] = v[0]
    logging.debug(f"Customer: {customer}")
    if "create_date" not in customer.keys():
        customer["create_date"] = datetime.now().isoformat()
    new_record = dbc.insert_customer_record(customer)
    logging.debug(f"New Record: {new_record}")
    return json.dumps(new_record)


@app.route("/customers", methods=["PUT"])
def update_customer():
    logging.debug(f"Form Data: {dict(request.form)}")
    customer = dict(dict(request.form).items())
    logging.debug(f"Customer: {customer}")
    new_record = dbc.update_customer_record(customer)
    logging.debug(f"New Record: {new_record}")
    return json.dumps(new_record)


@app.route("/", methods=["GET"])
def index():
    return render_template("index.html")


@app.route("/records", methods=["GET"])
def get_records():
    records = json.loads(get_customers())
    return render_template("records.html", results=records)


@app.route("/dbview", methods=["GET"])
def dbview():
    records = dbc.get_customer_records(raw=True)
    return render_template("dbview.html", results=records)


@app.route("/add", methods=["GET"])
def add():
    return render_template("add.html")


@app.route("/add", methods=["POST"])
def add_submit():
    records = create_customer()
    return render_template(
        "records.html", results=json.loads(records), record_added=True
    )


@app.route("/update", methods=["GET"])
def update():
    return render_template("update.html")


@app.route("/update", methods=["POST"])
def update_submit():
    records = update_customer()
    return render_template(
        "records.html", results=json.loads(records), record_updated=True
    )


def init_vault(conf) -> TransitClient:
    client = TransitClient()
    if not conf.has_section("VAULT") or conf["VAULT"]["Enabled"].lower() == "false":
        return client

    if (
        conf.has_option("VAULT", "Transform")
        and conf["VAULT"]["Transform"].lower() == "true"
    ):
        client = TransformClient()

    vault_token = ""
    if conf["VAULT"]["InjectToken"].lower() == "true":
        logger.info("Using Injected vault token")
        vault_token = read_vault_token()
    else:
        vault_token = conf["VAULT"]["Token"]

    client.init_vault(
        addr=conf["VAULT"]["Address"],
        token=vault_token,
        namespace=conf["VAULT"]["Namespace"],
        path=conf["VAULT"]["KeyPath"],
        key_name=conf["VAULT"]["KeyName"],
    )

    if (
        conf.has_option("VAULT", "Transform")
        and conf["VAULT"]["Transform"].lower() == "true"
    ):
        logger.info("Using Transform database client...")
        client.init_transform(
            transform_path=conf["VAULT"]["TransformPath"],
            ssn_role=conf["VAULT"]["SSNRole"],
            transform_masking_path=conf["VAULT"]["TransformMaskingPath"],
            ccn_role=conf["VAULT"]["CCNRole"],
        )
    if (
        conf.has_option("VAULT", "database_auth")
        and conf["VAULT"]["database_auth"] != ""
    ):
        client.vault_db_auth(conf["VAULT"]["database_auth"])

    return client


if __name__ == "__main__":
    logger.warning("In Main...")
    app_config = read_config()

    logging.basicConfig(
        level=log_level[app_config["DEFAULT"]["LogLevel"]],
        format="%(asctime)s - %(levelname)8s - %(name)9s - %(funcName)15s - %(message)s",
    )

    try:
        dbc = init_vault(app_config)
        if not dbc.is_initialized:
            logger.info("Using DB credentials from config.ini...")
            dbc.init_db(
                uri=app_config["DATABASE"]["Address"],
                prt=app_config["DATABASE"]["Port"],
                uname=app_config["DATABASE"]["User"],
                pw=app_config["DATABASE"]["Password"],
                db=app_config["DATABASE"]["Database"],
            )
        APP_HOST = "0.0.0.0"
        appPort = app_config["DEFAULT"]["port"]
        logger.info(f"Starting Flask server on {APP_HOST} listening on port {appPort}")
        app.run(host=APP_HOST, port=appPort)

    except Exception as e:
        logging.error(f"There was an error starting the server: {e}")
