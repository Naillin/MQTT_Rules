#!/bin/bash

# Проверка количества аргументов
if [ "$#" -ne 2 ]; then
	echo "Usage: $0 <fs/fb> <rule_numbers>"
	exit 1
fi

TYPE=$1
RULE_NUMBERS=$2

# Выбор файла в зависимости от типа
if [ "$TYPE" == "fb" ]; then
	FILE="rulesFirebase.json"
elif [ "$TYPE" == "fs" ]; then
	FILE="rulesFirestore.json"
else
	echo "Invalid type. Use 'fs' or 'fb'."
	exit 1
fi

# Преобразуем номера правил в массив
IFS=',' read -r -a RULES <<< "$RULE_NUMBERS"

# Удаление правил
for RULE in "${RULES[@]}"; do
	jq "del(.[$((RULE-1))])" "$FILE" > tmp.json && mv tmp.json "$FILE"
	echo "Rule $RULE deleted from $FILE."
done
