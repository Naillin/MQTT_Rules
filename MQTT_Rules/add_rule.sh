#!/bin/bash

# Проверка количества аргументов
if [ "$#" -ne 4 ]; then
    echo "Usage: $0 <fs/fb> <path/to/field> <direction_arrow> <path/to/topic>"
    echo "Example: $0 fs \"sw1/w6UuLzfcfWn4gpw66Aa8/sww1\" \">\" \"ss1\""
    exit 1
fi

TYPE=$1
FIELD_PATH=$2
DIRECTION_ARROW=$3
TOPIC_PATH=$4

# Выбор файла в зависимости от типа
if [ "$TYPE" == "fb" ]; then
    FILE="rulesFirebase.json"
elif [ "$TYPE" == "fs" ]; then
    FILE="rulesFirestore.json"
else
    echo "Invalid type. Use 'fs' or 'fb'."
    exit 1
fi

# Создание файла, если он не существует
if [ ! -f "$FILE" ]; then
    echo "[]" > "$FILE"
    echo "Created $FILE with an empty array."
fi

# Проверка, что файл содержит валидный JSON-массив
if ! jq empty "$FILE" 2>/dev/null; then
    echo "File $FILE contains invalid JSON. Resetting to an empty array."
    echo "[]" > "$FILE"
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

# Создание нового правила
NEW_RULE=$(jq -n \
    --arg ref "$FIELD_PATH" \
    --arg topic "$TOPIC_PATH" \
    --argjson dir "$DIRECTION" \
    '{FirebaseReference: $ref, MQTT_topic: $topic, Direction: $dir}')

# Добавление нового правила в файл
jq ". += [$NEW_RULE]" "$FILE" > tmp.json && mv tmp.json "$FILE"

echo "Rule added to $FILE."
