#!/bin/bash

# Имя сервиса
SERVICE_NAME="mqtt-rules.service"

# Получаем текущую директорию, где исполняется скрипт
CURRENT_DIR=$(pwd)

# Проверяем, существует ли сервис
if systemctl list-unit-files | grep -q "^$SERVICE_NAME"; then
	echo "Service $SERVICE_NAME already exists. Stopping and disabling it..."
	
	# Останавливаем сервис
	sudo systemctl stop $SERVICE_NAME
	
	# Отключаем сервис
	sudo systemctl disable $SERVICE_NAME
	
	# Удаляем файл сервиса
	sudo rm -f /etc/systemd/system/$SERVICE_NAME
	
	# Перезагружаем systemd
	sudo systemctl daemon-reload
	
	echo "Service $SERVICE_NAME has been stopped and removed."
fi

# Создаем содержимое для .service файла
SERVICE_CONTENT="[Unit]
Description=MQTT Rules
After=network.target

[Service]
ExecStart=$CURRENT_DIR/MQTT_Rules
WorkingDirectory=$CURRENT_DIR
Restart=always
User=$(whoami)

[Install]
WantedBy=multi-user.target"

# Создаем .service файл в /etc/systemd/system/
echo "$SERVICE_CONTENT" | sudo tee /etc/systemd/system/$SERVICE_NAME > /dev/null

# Перезагружаем systemd для применения изменений
sudo systemctl daemon-reload

# Включаем сервис
#sudo systemctl enable $SERVICE_NAME

# Запускаем сервис
sudo systemctl start $SERVICE_NAME

echo "Service $SERVICE_NAME has been created and started."
echo "If you need enable service for autorun. Execute 'sudo systemctl enable $SERVICE_NAME'."
