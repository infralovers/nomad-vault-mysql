import base64
import logging
import time

import mysql.connector
from mysql.connector import errorcode
import hvac

CUSTOMER_TABLE = """
CREATE TABLE IF NOT EXISTS `customers` (
    `cust_no` int(11) NOT NULL AUTO_INCREMENT,
    `birth_date` varchar(255) NOT NULL,
    `first_name` varchar(255) NOT NULL,
    `last_name` varchar(255) NOT NULL,
    `create_date` varchar(255) NOT NULL,
    `social_security_number` varchar(255) NOT NULL,
    `credit_card_number` varchar(255) NOT NULL,
    `address` varchar(255) NOT NULL,
    `salary` varchar(255) NOT NULL,
    PRIMARY KEY (`cust_no`)
) ENGINE=InnoDB;"""

SEED_CUSTOMERS = """
INSERT IGNORE into customers VALUES
  (2, "3/14/69", "Larry", "Johnson", "2020-01-01T14:49:12.301977", "360-56-6750", "3600-5600-6750-0000", "Tyler, Texas", "7000000"),
  (40, "11/26/69", "Shawn", "Kemp", "2020-02-21T10:24:55.985726", "235-32-8091", "2350-3200-8091-0001", "Elkhart, Indiana", "15000000"),
  (34, "2/20/63", "Charles", "Barkley", "2019-04-09T01:10:20.548144", "531-72-1553", "5310-7200-1553-0002", "Leeds, Alabama", "9000000");
"""

logger = logging.getLogger(__name__)


class DbClient:
    conn: mysql.connector.MySQLConnection = None
    uri: str = None
    port: int = None
    username: str = None
    password: str = None
    db: str = None

    vault_client: hvac.Client = None
    key_name: str = None
    mount_point: str = None
    namespace: str = None
    is_initialized: bool = False

    def init_db(self, uri, prt, uname, pw, db):
        self.connect_db(uri, prt, uname, pw)
        self._init_database(db)
        logger.info("database is initialized")

    def connect_db(self, uri, prt, uname, pw):
        for i in range(0, 10):
            try:
                logger.debug(
                    f"Connecting to {uri}:{prt} with username {uname} and password {pw}"
                )
                self.conn = mysql.connector.connect(
                    user=uname, password=pw, host=uri, port=prt
                )

                self.uri = uri
                self.port = prt
                self.username = uname
                self.password = pw
                return

            except mysql.connector.Error as err:
                if err.errno == errorcode.ER_ACCESS_DENIED_ERROR:
                    logger.error("Something is wrong with your user name or password")
                elif err.errno == errorcode.ER_BAD_DB_ERROR:
                    logger.error("Database does not exist")
                else:
                    logger.error(err)
                logger.debug("Sleeping 5 seconds before retry")
                time.sleep(3)

        raise ConnectionError(f"Could not connect {uri}:{prt} with user {uname}")

    def _init_database(self, db):
        cursor = self.conn.cursor()
        logger.info(f"Preparing database {db}...")
        cursor.execute(f"CREATE DATABASE IF NOT EXISTS `{db}`")
        cursor.execute(f"USE `{db}`")
        logger.info("Preparing customer table...")
        cursor.execute(CUSTOMER_TABLE)
        cursor.execute(SEED_CUSTOMERS)
        self.conn.commit()
        cursor.close()
        self.db = db
        self.is_initialized = True

    def get_namespace(self):
        return self.namespace

    # Later we will check to see if this is None to see whether to use Vault or not
    def init_vault(self, addr, token, namespace, path, key_name):
        if not addr or not token:
            logger.warning("Skipping initialization...")
            return
        logger.warning(f"Connecting to vault server: {addr}")
        self.vault_client = hvac.Client(
            url=addr, token=token, namespace=namespace, verify=False
        )
        self.namespace = namespace
        if not self.vault_client.is_authenticated():
            self.vault_client = None
            logger.error("could not authenticate to vault")
            return
        if key_name == "":
            key_name = None
        self.key_name = key_name
        self.mount_point = path
        logger.debug(f"Initialized vault_client: {self.vault_client}")

    def vault_db_auth(self, path):
        try:
            resp = self.vault_client.read(path)
            self.username = resp["data"]["username"]
            self.password = resp["data"]["password"]
            logger.debug(
                f"Retrieved username {self.username} and password {self.password} from Vault."
            )
        except Exception as e:
            logger.error(
                f"An error occurred reading DB creds from path {path}.  Error: {e}"
            )

    # the data must be base64ed before being passed to encrypt
    def encrypt(self, value):
        try:
            response = self.vault_client.secrets.transit.encrypt_data(
                mount_point=self.mount_point,
                name=self.key_name,
                plaintext=base64.b64encode(value.encode()).decode("ascii"),
            )
            logger.debug(f"Response: {response}")
            return response["data"]["ciphertext"]
        except Exception as e:
            logger.error(f"There was an error encrypting the data: {e}")
            raise e

    # The data returned from Transit is base64 encoded so we decode it before returning
    def decrypt(self, value):
        # support unencrypted messages on first read
        logger.debug(f"Decrypting {value}")
        if not value.startswith("vault:v"):
            return value
        try:
            response = self.vault_client.secrets.transit.decrypt_data(
                mount_point=self.mount_point, name=self.key_name, ciphertext=value
            )
            logger.debug(f"Response: {response}")
            plaintext = response["data"]["plaintext"]
            logger.debug(f"Plaintext (base64 encoded): {plaintext}")
            decoded = base64.b64decode(plaintext).decode()
            logger.debug(f"Decoded: {decoded}")
            return decoded
        except Exception as e:
            logger.error(f"There was an error encrypting the data: {e}")
            raise e

    # Long running apps may expire the DB connection
    def _execute_sql(self, sql, cursor):
        try:
            cursor.execute(sql)
        except mysql.connector.errors.OperationalError as error:
            if error[0] == 2006:
                logger.error(f"Error encountered: {error}.  Reconnecting db...")
                self.init_db(self.uri, self.port, self.username, self.password, self.db)
                cursor = self.conn.cursor()
                cursor.execute(sql)
                return 0
        return 1

    def process_customer(self, row, raw=None):
        r = {}
        r["customer_number"] = row[0]
        r["birth_date"] = row[1]
        r["first_name"] = row[2]
        r["last_name"] = row[3]
        r["create_date"] = row[4]
        r["ssn"] = row[5]
        r["ccn"] = row[6]
        r["address"] = row[7]
        r["salary"] = row[8]
        if self.vault_client is not None and not raw:
            r["birth_date"] = self.decrypt(r["birth_date"])
            r["ssn"] = self.decrypt(r["ssn"])
            r["ccn"] = self.decrypt(r["ccn"])
            r["address"] = self.decrypt(r["address"])
            r["salary"] = self.decrypt(r["salary"])
        return r

    def get_customer_records(self, num=None, raw=None):
        if num is None:
            num = 50
        statement = f"SELECT * FROM `customers` LIMIT {num}"
        cursor = self.conn.cursor()
        self._execute_sql(statement, cursor)
        results = []
        for row in cursor:
            try:
                r = self.process_customer(row, raw)
                results.append(r)
            except Exception as e:
                logger.error(f"There was an error retrieving the record: {e}")
        return results

    def get_customer_record(self, cid):
        statement = f"SELECT * FROM `customers` WHERE cust_no = {cid}"
        cursor = self.conn.cursor()
        self._execute_sql(statement, cursor)
        results = []
        for row in cursor:
            try:
                r = self.process_customer(row)
                results.append(r)
            except Exception as e:
                logger.error(f"There was an error retrieving the record: {e}")

        return results

    def get_insert_sql(self, record) -> str:
        if self.vault_client is None and self.key_name is None:
            return f"""INSERT INTO `customers` (`birth_date`, `first_name`, `last_name`, `create_date`, `social_security_number`, `credit_card_number`, `address`, `salary`)
                            VALUES  ("{record["birth_date"]}",
                            "{record["first_name"]}",
                            "{record["last_name"]}",
                            "{record["create_date"]}",
                            "{record["ssn"]}",
                            "{record["ccn"]}",
                            "{record["address"]}",
                            "{record["salary"]}");"""

        return f"""INSERT INTO `customers` (`birth_date`, `first_name`, `last_name`, `create_date`, `social_security_number`, `credit_card_number`, `address`, `salary`)
                        VALUES  ("{self.encrypt(record["birth_date"])}",
                        "{record["first_name"]}",
                        "{record["last_name"]}",
                        "{record["create_date"]}",
                        "{self.encrypt(record["ssn"])}",
                        "{self.encrypt(record["ccn"])}",
                        "{self.encrypt(record["address"])}",
                        "{self.encrypt(record["salary"])}");"""

    def insert_customer_record(self, record):
        statement = self.get_insert_sql(record)
        logger.debug(f"SQL Statement: {statement}")
        cursor = self.conn.cursor()
        self._execute_sql(statement, cursor)
        self.conn.commit()
        return self.get_customer_records()

    def get_update_sql(self, record) -> str:
        if self.vault_client is None:
            return f"""UPDATE `customers`
                       SET birth_date = "{record["birth_date"]}",
                       first_name = "{record["first_name"]}",
                       last_name = "{record["last_name"]}",
                       social_security_number = "{record["ssn"]}",
                       credit_card_number = "{record["ccn"]}",
                       address = "{record["address"]}",
                       salary = "{record["salary"]}"
                       WHERE cust_no = {record["cust_no"]};"""

        return f"""UPDATE `customers`
                       SET birth_date = "{self.encrypt(record["birth_date"])}",
                       first_name = "{record["first_name"]}",
                       last_name = "{record["last_name"]}",
                       social_security_number = "{self.encrypt(record["ssn"])}",
                       credit_card_number = "{self.encrypt(record["ccn"])}",
                       address = "{self.encrypt(record["address"])}",
                       salary = "{self.encrypt(record["salary"])}"
                       WHERE cust_no = {record["cust_no"]};"""

    def update_customer_record(self, record):
        statement = self.get_update_sql(record)
        logger.debug(f"Sql Statement: {statement}")
        cursor = self.conn.cursor()
        self._execute_sql(statement, cursor)
        self.conn.commit()
        return self.get_customer_records()
