#!/bin/bash

# Проверка количества аргументов
if [ "$#" -ne 1 ]; then
	echo "Usage: $0 <fs/fb>"
	exit 1
fi

TYPE=$1

# Выбор файла в зависимости от типа
if [ "$TYPE" == "fb" ]; then
	FILE="rulesFirebase.json"
elif [ "$TYPE" == "fs" ]; then
	FILE="rulesFirestore.json"
else
	echo "Invalid type. Use 'fs' or 'fb'."
	exit 1
fi

# Проверка существования файла
if [ ! -f "$FILE" ]; then
	echo "File $FILE does not exist."
	exit 1
fi

# Отображение правил с нумерацией в нужном формате
jq -r '
  to_entries | .[] | 
  "\(.key + 1). [\(.value.FirebaseReference) \(if .value.Direction then "<" else ">" end) \(.value.MQTT_topic)]"
' "$FILE"
