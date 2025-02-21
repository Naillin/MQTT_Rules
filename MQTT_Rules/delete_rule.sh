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

# Проверка существования файла
if [ ! -f "$FILE" ]; then
	echo "File $FILE does not exist."
	exit 1
fi

# Проверка валидности JSON
if ! jq empty "$FILE" 2>/dev/null; then
	echo "File $FILE contains invalid JSON."
	exit 1
fi

# Преобразуем номера правил в массив
IFS=',' read -r -a RULES <<< "$RULE_NUMBERS"

# Получаем общее количество правил
TOTAL_RULES=$(jq length "$FILE")

# Удаление правил
for RULE in "${RULES[@]}"; do
	# Проверка, что номер правила находится в допустимых пределах
	if [ "$RULE" -lt 1 ] || [ "$RULE" -gt "$TOTAL_RULES" ]; then
		echo "Rule $RULE does not exist in $FILE."
		continue
	fi
	jq "del(.[$((RULE-1))])" "$FILE" > tmp.json && mv tmp.json "$FILE"
	echo "Rule $RULE deleted from $FILE."
done
