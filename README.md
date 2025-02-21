# MQTT Rules Application

[Русская версия](README.ru.md)
---
[Windows](https://github.com/Naillin/MQTT_Client.git)

MQTT_Rules is an application that allows you to link Firebase/Firestore fields with MQTT topics using rules.

## Installation and Setup

1. First, you need to obtain a secret key if you plan to use Firebase. If you intend to use Firestore, you must obtain a `.json` file from the Firebase Admin SDK and enable the Cloud Firestore API for your project. You can use both databases simultaneously if needed.
2. You also need to install `jq` on your system (e.g., `sudo apt install jq` or `sudo pacman -S jq`).
3. Run the `create_files.sh` script. This script will create a `config.ini` file for configuring the program. You need to specify the following details:
   - Your MQTT broker details (address, port).
   - Login and password for connecting to the MQTT broker.
   - Firebase database URL.
   - Secret key for connecting to Firebase.
   - Project ID.
   - Path to the Firebase Admin SDK `.json` file.
   The script will also create two `.json` files to store the rules array.
4. Next, run the `initialization.sh` script. This script will create and start a daemon for running the application in the background. From this point, management is done using `systemctl`. If you need the daemon to start automatically on system boot, use the command `sudo systemctl enable mqtt-rules.service`.
5. If you need to remove MQTT_Rules, use the `stop_and_remove_service.sh` script. This script will stop the daemon and remove it from the system. You can then clean up the application's root directory.

## Key Features

### Rule Management

Link Firebase/Firestore database fields with MQTT topics. Rules allow automatic synchronization of data between Firebase/Firestore and MQTT. All rules are stored in the root directory in the `rulesFirebase.json`/`rulesFirestore.json` files. (Firebase field paths should start with a `/`, e.g., `/switch1/data`).
- **Create**: Use the `add_rule.sh` script. Example: `./add_rule.sh fb "path/to/field" ">" "path/to/topic"`. [Script] [fb/fs] [path/to/field] [direction] [path/to/topic] [true/false] [true/false]. That is, the script, the selection of the .json file associated with the interaction, the path to the field in the database, the direction of data movement, the path to the topic, the creation of a new field for new data from the topic, and the timestamp.
- **List**: Use the `list_rules.sh` script. Example: `./list_rules.sh fb`. [Script] [fb/fs]. This means: script, choice of `.json` to interact with.
- **Delete**: Use the `delete_rule.sh` script. Example: `./delete_rule.sh fb 1,2`. [Script] [fb/fs] [1.. 1,2,3..]. This means: script, choice of `.json` to interact with, rule number(s) to delete.
- **Delete All**: Use the `delete_all_rules.sh` script. Example: `./delete_rule.sh`. This script will delete all rules in both `.json` arrays.
- **IMPORTANT!!!**: Rules only work with string-type fields! You cannot use paths to collections or roots in Firebase/Firestore! This may lead to errors or unexpected behavior in rule execution! Note that the program works with data in string representation. If you create a field with a numeric type, it will be converted to a string after being rewritten by a rule.

## Usage Example

```bash
./create_files.sh
sudo nano config.ini
./add_rule.sh fb "switch1/sw1" ">" "switch1/topicSW1" false false
./add_rule.sh fb "switch1/sw1" "<" "switch1/topicSW1" false false
./initialization.sh
journalctl -xu mqtt-rules.service
sudo systemctl stop mqtt-rules.service
```
This snippet demonstrates the creation of a group of rules that implement synchronization between the database field `switch1/sw1` and the topic `switch1/topicSW1`. The code includes: creating the necessary files, configuring the setup, adding rules, starting the daemon, reading logs, and stopping the daemon.

## License

MIT License.
