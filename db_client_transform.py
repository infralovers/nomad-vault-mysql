import logging
import requests
from db_client import DbClient as TransitDBClient

logger = logging.getLogger(__name__)


class DbClient(TransitDBClient):
    transform_mount_point = None
    transform_masking_mount_point = None
    ssn_role = None
    ccn_role = None

    # Later we will check to see if this is None to see whether to use Vault or not
    def init_transform(
        self,
        transform_path,
        transform_masking_path,
        ssn_role,
        ccn_role,
    ):
        self.transform_mount_point = transform_path
        self.transform_masking_mount_point = transform_masking_path
        self.ssn_role = ssn_role
        self.ccn_role = ccn_role
        logger.debug(f"Initialized transform: {self.vault_client}")

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
                "X-Vault-Namespace": super().get_namespace(),
                "Content-Type": "application/json",
                "cache-control": "no-cache",
            }

            response = requests.request("POST", url, data=payload, headers=headers, timeout=300)
            logger.debug(f"Response: {response.text}")
            return response.json()["data"]["encoded_value"]
        except Exception as e:
            logger.error(f"There was an error encrypting the data: {e}")

        return ""

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
                "X-Vault-Namespace": self.get_namespace(),
                "Content-Type": "application/json",
                "cache-control": "no-cache",
            }

            response = requests.request("POST", url, data=payload, headers=headers, timeout=300)
            logger.debug(f"Response: {response.text}")
            return response.json()["data"]["encoded_value"]
        except Exception as e:
            logger.error(f"There was an error encrypting the data: {e}")

        return ""

    def decode_ssn(self, value):
        # we're going to have funny stuff if ProtectRecords is false
        logger.debug(f"Decoding {value}")
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
                "X-Vault-Namespace": self.get_namespace(),
                "Content-Type": "application/json",
                "cache-control": "no-cache",
            }

            response = requests.request(
                "POST", url, data=payload, headers=headers, timeout=300
            )
            logger.debug(f"Response: {response.text}")
            return response.json()["data"]["decoded_value"]
        except Exception as e:
            logger.error(f"There was an error decoding the data: {e}")
        return None

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

        return f"""INSERT INTO `customers` (`birth_date`, `first_name`, `last_name`, `create_date`,
                    `social_security_number`, `credit_card_number`, `address`, `salary`)
                            VALUES  ("{self.encrypt(record["birth_date"])}",
                                     "{record["first_name"]}",
                                     "{record["last_name"]}",
                                     "{record["create_date"]}",
                                     "{self.encode_ssn(record["ssn"])}",
                                     "{self.encode_ccn(record["ccn"])}",
                                     "{self.encrypt(record["address"])}",
                                     "{self.encrypt(record["salary"])}");"""

    def get_update_sql(self, record) -> str:
        if self.vault_client is None:
            return super().get_update_sql(record)

        return f"""UPDATE `customers`
                    SET birth_date = "{self.encrypt(record["birth_date"])}", f
                    irst_name = "{record["first_name"]}",
                    last_name = "{record["last_name"]}",
                    social_security_number = "{self.encode_ssn(record["ssn"])}",
                    credit_card_number = "{self.encode_ccn(record["ccn"])}",
                    address = "{self.encrypt(record["address"])}",
                    salary = "{self.encrypt(record["salary"])}"
                    WHERE cust_no = {record["cust_no"]};"""
