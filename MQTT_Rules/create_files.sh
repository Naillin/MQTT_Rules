#!/bin/bash

# Удаление старых файлов, если они существуют
if [ -f "config.ini" ]; then
	rm config.ini
	echo "Old config.ini deleted."
fi

if [ -f "rulesFirebase.json" ]; then
	rm rulesFirebase.json
	echo "Old rulesFirebase.json deleted."
fi

if [ -f "rulesFirestore.json" ]; then
	rm rulesFirestore.json
	echo "Old rulesFirestore.json deleted."
fi

# Создание config.ini
cat <<EOL > config.ini
[Settings]
ADDRESS = 127.0.0.1
PORT = 1883
LOGIN = user_login
PASSWORD = user_password
URL_FIREBASE = 
SECRET_FIREBASE = 
ID_FIRESTORE = 
PATH_FIRESTORE = 
EOL

# Создание rulesFirebase.json
echo "[]" > rulesFirebase.json

# Создание rulesFirestore.json
echo "[]" > rulesFirestore.json

# Вывод сообщения об успешном завершении
echo "Files config.ini, rulesFirebase.json and rulesFirestore.json created."
