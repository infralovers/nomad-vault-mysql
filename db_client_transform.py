import logging
import requests
from db_client import DbClient as TransitDBClient

customer_table = """
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

seed_customers = """
INSERT IGNORE into customers VALUES
  (2, "3/14/69", "Larry", "Johnson", "2020-01-01T14:49:12.301977", "360-56-6750", "3600-5600-6750-0000", "Tyler, Texas", "7000000"),
  (40, "11/26/69", "Shawn", "Kemp", "2020-02-21T10:24:55.985726", "235-32-8091", "2350-3200-8091-0001", "Elkhart, Indiana", "15000000"),
  (34, "2/20/63", "Charles", "Barkley", "2019-04-09T01:10:20.548144", "531-72-1553", "5310-7200-1553-0002", "Leeds, Alabama", "9000000");
"""
logger = logging.getLogger(__name__)


class DbClient(TransitDBClient):
    transform_mount_point = None
    transform_masking_mount_point = None
    ssn_role = None
    ccn_role = None



    # Later we will check to see if this is None to see whether to use Vault or not
    def init_vault(
        self,
        addr,
        token,
        namespace,
        path,
        key_name,
        transform_path,
        transform_masking_path,
        ssn_role,
        ccn_role,
    ):
        super().init_vault(
            addr=addr, token=token, namespace=namespace, path=path, key_name=key_name
        )
        self.transform_mount_point = transform_path
        self.transform_masking_mount_point = transform_masking_path
        self.ssn_role = ssn_role
        self.ccn_role = ccn_role
        self.namespace = namespace
        self.token = token
        logger.debug("Initialized vault_client: {}".format(self.vault_client))

    def encode_ssn(self, value):
        try:
            # transform not available in hvac, raw api call
            url = (
                self.vault_client.url
                + "/v1/"
                + self.transform_mount_point
                + "/encode/"
                + self.ssn_role
            )
            payload = (
                '{\n  "value": "'
                + value
                + '",\n  "transformation": "'
                + self.ssn_role
                + '"\n}'
            )
            headers = {
                "X-Vault-Token": self.vault_client.token,
                "X-Vault-Namespace": self.namespace,
                "Content-Type": "application/json",
                "cache-control": "no-cache",
            }

            response = requests.request("POST", url, data=payload, headers=headers)
            logger.debug("Response: {}".format(response.text))
            return response.json()["data"]["encoded_value"]
        except Exception as e:
            logger.error("There was an error encrypting the data: {}".format(e))

    def encode_ccn(self, value):
        try:
            # transform not available in hvac, raw api call
            url = (
                self.vault_client.url
                + "/v1/"
                + self.transform_masking_mount_point
                + "/encode/"
                + self.ccn_role
            )
            payload = (
                '{\n  "value": "'
                + value
                + '",\n  "transformation": "'
                + self.ccn_role
                + '"\n}'
            )
            headers = {
                "X-Vault-Token": self.vault_client.token,
                "X-Vault-Namespace": self.namespace,
                "Content-Type": "application/json",
                "cache-control": "no-cache",
            }

            response = requests.request("POST", url, data=payload, headers=headers)
            logger.debug("Response: {}".format(response.text))
            return response.json()["data"]["encoded_value"]
        except Exception as e:
            logger.error("There was an error encrypting the data: {}".format(e))

    def decode_ssn(self, value):
        # we're going to have funny stuff if ProtectRecords is false
        logger.debug("Decoding {}".format(value))
        try:
            # transform not available in hvac, raw api call
            url = (
                self.vault_client.url
                + "/v1/"
                + self.transform_mount_point
                + "/decode/"
                + self.ssn_role
            )
            payload = (
                '{\n  "value": "'
                + value
                + '",\n  "transformation": "'
                + self.ssn_role
                + '"\n}'
            )
            headers = {
                "X-Vault-Token": self.vault_client.token,
                "X-Vault-Namespace": self.namespace,
                "Content-Type": "application/json",
                "cache-control": "no-cache",
            }

            response = requests.request("POST", url, data=payload, headers=headers)
            logger.debug("Response: {}".format(response.text))
            return response.json()["data"]["decoded_value"]
        except Exception as e:
            logger.error("There was an error decoding the data: {}".format(e))

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
            r["ssn"] = self.decode_ssn(r["ssn"])
            r["address"] = self.decrypt(r["address"])
            r["salary"] = self.decrypt(r["salary"])
        return r

    def get_insert_sql(self, record) -> str:
        if self.vault_client is None:
            return super().get_insert_sql(record)

        return """INSERT INTO `customers` (`birth_date`, `first_name`, `last_name`, `create_date`, `social_security_number`, `credit_card_number`, `address`, `salary`)
                            VALUES  ("{}", "{}", "{}", "{}", "{}", "{}", "{}","{}");""".format(
            self.encrypt(record["birth_date"]),
            record["first_name"],
            record["last_name"],
            record["create_date"],
            self.encode_ssn(record["ssn"]),
            self.encode_ccn(record["ccn"]),
            self.encrypt(record["address"]),
            self.encrypt(record["salary"]),
        )

    def get_update_sql(self, record) -> str:
        if self.vault_client is None:
            return super().get_update_sql(record)

        return """UPDATE `customers`
                    SET birth_date = "{}", first_name = "{}", last_name = "{}", social_security_number = "{}", credit_card_number = "{}", address = "{}", salary = "{}"
                    WHERE cust_no = {};""".format(
            self.encrypt(record["birth_date"]),
            record["first_name"],
            record["last_name"],
            self.encode_ssn(record["ssn"]),
            self.encode_ccn(record["ccn"]),
            self.encrypt(record["address"]),
            self.encrypt(record["salary"]),
            record["cust_no"],
        )
