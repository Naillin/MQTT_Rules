#!/bin/bash

# �������� ���������� ����������
if [ "$#" -ne 4 ]; then
    echo "Usage: $0 <fs/fb> <path/to/field> <direction_arrow> <path/to/topic>"
    echo "Example: $0 fs \"sw1/w6UuLzfcfWn4gpw66Aa8/sww1\" \">\" \"ss1\""
    exit 1
fi

TYPE=$1
FIELD_PATH=$2
DIRECTION_ARROW=$3
TOPIC_PATH=$4

# ����� ����� � ����������� �� ����
if [ "$TYPE" == "fb" ]; then
    FILE="rulesFirebase.json"
elif [ "$TYPE" == "fs" ]; then
    FILE="rulesFirestore.json"
else
    echo "Invalid type. Use 'fs' or 'fb'."
    exit 1
fi

# �������� �����, ���� �� �� ����������
if [ ! -f "$FILE" ]; then
    echo "[]" > "$FILE"
    echo "Created $FILE with an empty array."
fi

# ��������, ��� ���� �������� �������� JSON-������
if ! jq empty "$FILE" 2>/dev/null; then
    echo "File $FILE contains invalid JSON. Resetting to an empty array."
    echo "[]" > "$FILE"
fi

# �������������� ������� ����������� � true/false
if [ "$DIRECTION_ARROW" == ">" ]; then
    DIRECTION="false"
elif [ "$DIRECTION_ARROW" == "<" ]; then
    DIRECTION="true"
else
    echo "Invalid direction. Use '>' or '<'."
    exit 1
fi

# �������� ������ �������
NEW_RULE=$(jq -n \
    --arg ref "$FIELD_PATH" \
    --arg topic "$TOPIC_PATH" \
    --argjson dir "$DIRECTION" \
    '{FirebaseReference: $ref, MQTT_topic: $topic, Direction: $dir}')

# ���������� ������ ������� � ����
jq ". += [$NEW_RULE]" "$FILE" > tmp.json && mv tmp.json "$FILE"

echo "Rule added to $FILE."
