using IniParser.Model;
using IniParser;
using NLog;
using MQTT_Rules.Rule;
using Newtonsoft.Json;
using MQTT_Rules.FirebaseTools;
using System.Runtime.InteropServices;
using System.Data;

namespace MQTT_Rules
{
    internal class Program
    {
		private static readonly string moduleName = "Program";
		private static readonly Logger baseLogger = LogManager.GetLogger(moduleName);
		private static readonly LoggerManager logger = new LoggerManager(baseLogger, moduleName);

		private string ADDRESS = "127.0.0.1";
		private int PORT = 1883;
		private string LOGIN = "user_login";
		private string PASSWORD = "user_password";
		private string URL_FIREBASE = string.Empty;
		private string SECRET_FIREBASE = string.Empty;
		private string ID_FIRESTORE = string.Empty;
		private string PATH_FIRESTORE = string.Empty;
		private const string filePathConfig = "config.ini";
		private const string filePathFirebaseRules = "rulesFirebase.json";
		private const string filePathFirestoreRules = "rulesFirestore.json";

		private string configTextDefault = string.Empty;
		private void initConfig()
		{
			FileIniDataParser parser = new FileIniDataParser();

			if (File.Exists(filePathConfig))
			{
				IniData data = parser.ReadFile(filePathConfig);

				string[] linesConfig = File.ReadAllLines(filePathConfig);
				ADDRESS = data["Settings"]["ADDRESS"];
				PORT = Convert.ToInt32(data["Settings"]["PORT"]);
				LOGIN = data["Settings"]["LOGIN"];
				PASSWORD = data["Settings"]["PASSWORD"];
				URL_FIREBASE = data["Settings"]["URL_FIREBASE"];
				SECRET_FIREBASE = data["Settings"]["SECRET_FIREBASE"];
				ID_FIRESTORE = data["Settings"]["ID_FIRESTORE"];
				PATH_FIRESTORE = data["Settings"]["PATH_FIRESTORE"];
			}
			else
			{
				IniData data = new IniData();
				data.Sections.AddSection("Settings");
				data["Settings"]["ADDRESS"] = ADDRESS;
				data["Settings"]["PORT"] = PORT.ToString();
				data["Settings"]["LOGIN"] = LOGIN;
				data["Settings"]["PASSWORD"] = PASSWORD;
				data["Settings"]["URL_FIREBASE"] = URL_FIREBASE;
				data["Settings"]["SECRET_FIREBASE"] = SECRET_FIREBASE;
				data["Settings"]["ID_FIRESTORE"] = ID_FIRESTORE;
				data["Settings"]["PATH_FIRESTORE"] = PATH_FIRESTORE;
				parser.WriteFile(filePathConfig, data);
			}

			configTextDefault = $"ADDRESS = [{ADDRESS}]\r\n" +
								$"PORT = [{PORT.ToString()}]\r\n" +
								$"LOGIN = [{LOGIN}]\r\n" +
								$"PASSWORD = [{PASSWORD}]\n\r" +
								$"URL_FIREBASE = [{URL_FIREBASE}]\n\r" +
								$"SECRET_FIREBASE = [{SECRET_FIREBASE}]\n\r" +
								$"ID_FIRESTORE = [{ID_FIRESTORE}]\n\r" +
								$"PATH_FIRESTORE = [{PATH_FIRESTORE}]";
		}

		private static void loadRules(string path, Action<string, string, bool> AddRule)
		{
			if (File.Exists(path))
			{
				string json = File.ReadAllText(path);
				List<RuleUnit>? jsonRules = JsonConvert.DeserializeObject<List<RuleUnit>>(json);
				if (jsonRules != null && jsonRules.Count != 0)
				{
					foreach (RuleUnit rule in jsonRules)
					{
						AddRule(rule.FirebaseReference, rule.MQTT_topic, rule.Direction);
					}
				}
			}
			else
			{
				File.Create(path).Close();
			}
		}

		private static FirebaseService? firebaseService = null;
		private static MqttBrokerClient? mqttReciverClientFirebase = null;
		private static Dictionary<string, string> subscriptionsFirebase = new Dictionary<string, string>();

		private static FirestoreService? firestoreService = null;
		private static MqttBrokerClient? mqttReciverClientFirestore = null;
		static void Main(string[] args)
		{
			logger.Info("Start MQTT Rules.");

			Program p = new Program();
			p.initConfig();

			logger.Info(p.configTextDefault);

			logger.Info("All done!");

			try
			{
				AppDomain.CurrentDomain.ProcessExit += OnProcessExit; // Для ProcessExit
				Console.CancelKeyPress += OnCancelKeyPress;          // Для Ctrl+C (SIGINT)

				// Подписываемся на SIGTERM (только для Linux)
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				{
					UnixSignalHandler.Register(Signum.SIGTERM, OnSigTerm);
				}

				//MQTTReciverFirebase
				if (!string.IsNullOrEmpty(p.URL_FIREBASE) && string.IsNullOrEmpty(p.SECRET_FIREBASE))
				{
					firebaseService = new FirebaseService(p.URL_FIREBASE, p.SECRET_FIREBASE);
					mqttReciverClientFirebase = new MqttBrokerClient(p.ADDRESS, p.PORT, p.LOGIN, p.PASSWORD);

					mqttReciverClientFirebase.Connect();
					loadRules(filePathFirebaseRules, AddRuleFirebase);
					logger.Info("MQTT Reciver for Firebase is ready");
					Task.Run(() =>
					{
						mqttReciverClientFirebase.MessageReceived += async (senderMQTT, eMQTT) =>
						{
							logger.Info($"Message recived: Topic: [{eMQTT.Topic}] Message: [{eMQTT.Payload}]");

							RuleControl? rule;
							lock (ruleControlsFirebase)
							{
								List<RuleControl> rules = ruleControlsFirebase.Where(r => r.Direction == true).ToList();
								rule = rules.FirstOrDefault(r => r.MQTT_topic == eMQTT.Topic);
							}

							if (rule != null && eMQTT.Payload != null)
								await firebaseService.UpdateDataAsync<string>(rule.FirebaseReference, eMQTT.Payload);
						};
					});

					//Firebase
					Task.Run(async () =>
					{
						while (true)
						{
							List<RuleControl> rules;
							lock (ruleControlsFirebase)
							{
								rules = ruleControlsFirebase.Where(r => r.Direction == false).ToList(); // Создаём копию списка
							}
							foreach (RuleControl rule in rules)
							{
								string firebaseData = await firebaseService.GetDataAsync<string>(rule.FirebaseReference);
								if (!subscriptionsFirebase.TryGetValue(rule.FirebaseReference, out string? oldValue) || oldValue != firebaseData)
								{
									logger.Info($"Message recived: Firebase path: [{rule.FirebaseReference}] Message: [{firebaseData}]");

									subscriptionsFirebase[rule.FirebaseReference] = firebaseData;
									mqttReciverClientFirebase.Publish(rule.MQTT_topic, firebaseData);
								}
							}

							await Task.Delay(3000);
						}
					});
				}
				else
				{
					logger.Warn($"Firebase is not started.");
				}

				//MQTTReciverFirestore
				if (!string.IsNullOrEmpty(p.ID_FIRESTORE) && string.IsNullOrEmpty(p.PATH_FIRESTORE))
				{
					firestoreService = new FirestoreService(p.ID_FIRESTORE, p.PATH_FIRESTORE);
					mqttReciverClientFirestore = new MqttBrokerClient(p.ADDRESS, p.PORT, p.LOGIN, p.PASSWORD);

					mqttReciverClientFirestore.Connect();
					loadRules(filePathFirestoreRules, AddRuleFirestore);
					logger.Info("MQTT Reciver for Firestore is ready");
					Task.Run(() =>
					{
						mqttReciverClientFirestore.MessageReceived += async (senderMQTT, eMQTT) =>
						{
							logger.Info($"Message recived: Topic: [{eMQTT.Topic}] Message: [{eMQTT.Payload}]");

							RuleControl? rule;
							lock (ruleControlsFirebase)
							{
								List<RuleControl> rules = ruleControlsFirestore.Where(r => r.Direction == true).ToList();
								rule = rules.FirstOrDefault(r => r.MQTT_topic == eMQTT.Topic);
							}

							if (rule != null && eMQTT.Payload != null)
							{
								FirestorePath path = new FirestorePath(rule.FirebaseReference);
								var updates = new Dictionary<string, object>
							{
								{ path.Field, eMQTT.Payload }
							};
								await firestoreService.UpdateDataAsync(path, updates);
							}
						};
					});

					//Firestore
					Task.Run(() =>
					{
						firestoreService.OnMessage += (senderFirestore, eFirestore) =>
						{
							if (eFirestore.Path != null && eFirestore.Data != null)
							{
								logger.Info($"Message recived: Firestore path: [{eFirestore.Path.SourcePath}] Message: [{eFirestore.Data.ToString()}]");

								List<RuleControl> rules;
								lock (ruleControlsFirestore)
								{
									rules = ruleControlsFirestore.Where(r => r.Direction == false).ToList(); // Создаём копию списка
								}
								RuleControl? rule = rules.FirstOrDefault(r => r.FirebaseReference == eFirestore.Path.SourcePath);

								if (rule != null)
									mqttReciverClientFirestore.Publish(rule.MQTT_topic, eFirestore.Data.ToString()!);
							}
						};
					});
				}
				else
				{
					logger.Warn($"Firestore is not started.");
				}
			}
			catch (Exception ex)
			{
				logger.Error($"Error: {ex.Message}");
				Environment.Exit(0);
			}
		}

		//----------------------------------------------------- FIREBASE RULES -----------------------------------------------------

		private static List<RuleControl> ruleControlsFirebase = new List<RuleControl>();
		private static void AddRuleFirebase(string FirebaseReference, string MQTT_topic, bool Direction)
		{
			// Создаем новый элемент управления
			RuleControl ruleControl = new RuleControl(FirebaseReference, MQTT_topic, Direction);
			// Добавляем его в список для управления
			ruleControlsFirebase.Add(ruleControl);

			if (mqttReciverClientFirebase != null && firestoreService != null)
			{
				if (ruleControl.Direction)
					mqttReciverClientFirebase.Subscribe(ruleControl.MQTT_topic);

				logger.Info($"Activate rule: [{ruleControl.FirebaseReference}] {(ruleControl.Direction ? "<" : ">")} [{ruleControl.MQTT_topic}].");
			}
		}

		//----------------------------------------------------- FIRESTORE RULES -----------------------------------------------------

		private static List<RuleControl> ruleControlsFirestore = new List<RuleControl>();
		private static void AddRuleFirestore(string FirestoreReference, string MQTT_topic, bool Direction)
		{
			// Создаем новый элемент управления
			RuleControl ruleControl = new RuleControl(FirestoreReference, MQTT_topic, Direction);
			// Добавляем его в список для управления
			ruleControlsFirestore.Add(ruleControl);

			if (mqttReciverClientFirestore != null && firestoreService != null)
			{
				mqttReciverClientFirestore.Subscribe(ruleControl.MQTT_topic);

				if (ruleControl.Direction)
					mqttReciverClientFirestore.Subscribe(ruleControl.MQTT_topic);
				else
					firestoreService.Subscribe(new FirestorePath(ruleControl.FirebaseReference));

				logger.Info($"Activate rule: [{ruleControl.FirebaseReference}] {(ruleControl.Direction ? "<" : ">")} [{ruleControl.MQTT_topic}].");
			}
		}

		//----------------------------------------------------- SYSTEM -----------------------------------------------------

		private static void DisconnectAll()
		{
			if (mqttReciverClientFirebase != null)
			{
				if (mqttReciverClientFirebase.IsConnected())
				{
					foreach (RuleControl rule in ruleControlsFirebase)
					{
						mqttReciverClientFirebase.Unsubscribe(rule.MQTT_topic);
					}

					mqttReciverClientFirebase.Disconnect();
				}
			}

			if (mqttReciverClientFirestore != null)
			{
				if (mqttReciverClientFirestore.IsConnected())
				{
					foreach (RuleControl rule in ruleControlsFirebase)
					{
						mqttReciverClientFirestore.Unsubscribe(rule.MQTT_topic);
					}
					
					mqttReciverClientFirestore.Disconnect();
				}
			}

			if (firestoreService != null)
			{
				firestoreService.UnsubscribeAll();
			}
		}

		private static bool _isExiting = false; // Флаг для отслеживания состояния завершения
		private static readonly object _lock = new object(); // Объект для блокировки
		private static void OnProcessExit(object? sender, EventArgs e)
		{
			lock (_lock)
			{
				if (_isExiting) return; // Если уже завершаемся, выходим
				_isExiting = true; // Устанавливаем флаг
			}

			logger.Info("Обработчик ProcessExit: завершение работы...");

			try
			{
				DisconnectAll();
			}
			catch (Exception ex)
			{
				logger.Error($"Ошибка завершения работы: {ex.Message}");
			}
			finally
			{
				Environment.Exit(0); // Завершаем программу
			}
		}

		private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
		{
			lock (_lock)
			{
				if (_isExiting) return; // Если уже завершаемся, выходим
				_isExiting = true; // Устанавливаем флаг
			}

			logger.Info("Обработчик Ctrl+C (SIGINT): завершение работы...");
			e.Cancel = true; // Предотвращаем завершение процесса по умолчанию

			try
			{
				DisconnectAll();
			}
			catch (Exception ex)
			{
				logger.Error($"Ошибка завершения работы: {ex.Message}");
			}
			finally
			{
				Environment.Exit(0); // Завершаем программу
			}
		}

		private static void OnSigTerm()
		{
			lock (_lock)
			{
				if (_isExiting) return; // Если уже завершаемся, выходим
				_isExiting = true; // Устанавливаем флаг
			}

			logger.Info("Обработчик SIGTERM: завершение работы...");

			try
			{
				DisconnectAll();
			}
			catch (Exception ex)
			{
				logger.Error($"Ошибка завершения работы: {ex.Message}");
			}
			finally
			{
				Environment.Exit(0); // Завершаем программу
			}
		}
	}
}
