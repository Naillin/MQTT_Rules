#!/bin/bash

# Проверка количества аргументов
if [ "$#" -lt 4 ] || [ "$#" -gt 6 ]; then
	echo "Usage: $0 <fs/fb> <path/to/field> <direction_arrow> <path/to/topic> [NewField] [Timestamp]"
	echo "Example: $0 fs \"sw1/document/sww1\" \">\" \"ss1/sww1\""
	echo "Example: $0 fb \"sw1/document/sww1\" \"<\" \"ss1/sww1\" true"
	echo "Example: $0 fs \"sw1/document/sww1\" \">\" \"ss1/sww1\" true false"
	exit 1
fi

TYPE=$1
FIELD_PATH=$2
DIRECTION_ARROW=$3
TOPIC_PATH=$4
NEW_FIELD=${5:-false}
TIMESTAMP=${6:-false}

# Проверка, что если NewField != true, то Timestamp должен быть false
if [ "$NEW_FIELD" != "true" ] && [ "$TIMESTAMP" == "true" ]; then
	echo "Error: Timestamp can only be true if NewField is true."
	exit 1
fi

# Преобразование стрелки направления в true/false
if [ "$DIRECTION_ARROW" == ">" ]; then
	DIRECTION="false"
elif [ "$DIRECTION_ARROW" == "<" ]; then
	DIRECTION="true"
else
	echo "Invalid direction. Use '>' or '<'."
	exit 1
fi

# Выбор файла в зависимости от типа
if [ "$TYPE" == "fb" ]; then
	FILE="rulesFirebase.json"
elif [ "$TYPE" == "fs" ]; then
	FILE="rulesFirestore.json"
else
	echo "Invalid type. Use 'fs' or 'fb'."
	exit 1
fi

# Создание нового правила
NEW_RULE=$(jq -n \
	--arg ref "$FIELD_PATH" \
	--arg topic "$TOPIC_PATH" \
	--argjson dir "$DIRECTION" \
	--argjson newField "$NEW_FIELD" \
	--argjson timestamp "$TIMESTAMP" \
	'{FirebaseReference: $ref, MQTT_topic: $topic, Direction: $dir, NewField: $newField, Timestamp: $timestamp}')

# Добавление нового правила в файл
if [ ! -f "$FILE" ]; then
	echo "[]" > "$FILE"
	echo "Created $FILE with an empty array."
fi

if ! jq empty "$FILE" 2>/dev/null; then
	echo "File $FILE contains invalid JSON. Resetting to an empty array."
	echo "[]" > "$FILE"
fi

jq ". += [$NEW_RULE]" "$FILE" > tmp.json && mv tmp.json "$FILE"

echo "Rule added to $FILE."
