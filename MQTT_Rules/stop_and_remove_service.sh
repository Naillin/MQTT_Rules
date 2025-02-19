#!/bin/bash

# Имя сервиса
SERVICE_NAME="mqtt-rules.service"

# Остановка сервиса, если он запущен
if systemctl is-active --quiet "$SERVICE_NAME"; then
	echo "Stopping $SERVICE_NAME..."
	sudo systemctl stop "$SERVICE_NAME"
	echo "$SERVICE_NAME stopped."
else
	echo "$SERVICE_NAME is not running."
fi

# Отключение сервиса, если он включен
if systemctl is-enabled --quiet "$SERVICE_NAME"; then
	echo "Disabling $SERVICE_NAME..."
	sudo systemctl disable "$SERVICE_NAME"
	echo "$SERVICE_NAME disabled."
else
	echo "$SERVICE_NAME is not enabled."
fi

# Удаление файла сервиса, если он существует
SERVICE_FILE="/etc/systemd/system/$SERVICE_NAME"
if [ -f "$SERVICE_FILE" ]; then
	echo "Removing $SERVICE_FILE..."
	sudo rm -f "$SERVICE_FILE"
	echo "$SERVICE_FILE removed."
else
	echo "$SERVICE_FILE does not exist."
fi

# Перезагрузка systemd для применения изменений
sudo systemctl daemon-reload
echo "Systemd daemon reloaded."

echo "Service $SERVICE_NAME has been stopped and removed."
