#!/bin/bash

# �������� ���������� ����������
if [ "$#" -ne 1 ]; then
	echo "Usage: $0 <fs/fb>"
	exit 1
fi

TYPE=$1

# ����� ����� � ����������� �� ����
if [ "$TYPE" == "fb" ]; then
	FILE="rulesFirebase.json"
elif [ "$TYPE" == "fs" ]; then
	FILE="rulesFirestore.json"
else
	echo "Invalid type. Use 'fs' or 'fb'."
	exit 1
fi

# �������� ������������� �����
if [ ! -f "$FILE" ]; then
	echo "File $FILE does not exist."
	exit 1
fi

# ����������� ������ � ���������� � ������ �������
jq -r '
  to_entries | .[] | 
  "\(.key + 1). [\(.value.FirebaseReference) \(if .value.Direction then "<" else ">" end) \(.value.MQTT_topic)]"
' "$FILE"
