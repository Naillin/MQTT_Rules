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

			configTextDefault = $"ADDRESS = [{ADDRESS}]\n" +
								$"PORT = [{PORT.ToString()}]\n" +
								$"LOGIN = [{LOGIN}]\n" +
								$"PASSWORD = [{PASSWORD}]\n" +
								$"URL_FIREBASE = [{URL_FIREBASE}]\n" +
								$"SECRET_FIREBASE = [{SECRET_FIREBASE}]\n" +
								$"ID_FIRESTORE = [{ID_FIRESTORE}]\n" +
								$"PATH_FIRESTORE = [{PATH_FIRESTORE}]";
		}

		private static void loadRules(string path, Action<string, string, bool, bool, bool> AddRule)
		{
			if (File.Exists(path))
			{
				string json = File.ReadAllText(path);
				List<RuleUnit>? jsonRules = JsonConvert.DeserializeObject<List<RuleUnit>>(json);
				if (jsonRules != null && jsonRules.Count != 0)
				{
					foreach (RuleUnit rule in jsonRules)
					{
						AddRule(rule.FirebaseReference, rule.MQTT_topic, rule.Direction, rule.NewField, rule.Timestamp);
					}
				}
			}
			else
			{
				File.Create(path).Close();
				File.WriteAllText(path, "[]");
			}
		}

		private static FirebaseService? firebaseService = null;
		private static MqttBrokerClient? mqttReciverClientFirebase = null;
		private static Dictionary<string, string> subscriptionsFirebase = new Dictionary<string, string>();
		private static Dictionary<string, int> countFiledsFirebase = new Dictionary<string, int>();

		private static FirestoreService? firestoreService = null;
		private static MqttBrokerClient? mqttReciverClientFirestore = null;
		private static Dictionary<string, int> countFiledsFirestore = new Dictionary<string, int>();
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
				if (!string.IsNullOrEmpty(p.URL_FIREBASE) && !string.IsNullOrEmpty(p.SECRET_FIREBASE))
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

							if (rule != null && !string.IsNullOrEmpty(eMQTT.Payload))
							{
								if (!string.IsNullOrEmpty(rule.FirebaseReference))
								{
									string data = eMQTT.Payload;
									if (rule.Timestamp)
									{
										data = data.Split('|')[0];
										data += $"|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
									}

									string fbRef = rule.FirebaseReference;
									string lastElement = fbRef.TrimEnd('/').Split('/').Last();
									if (rule.NewField)
									{
										if (await firebaseService.IsNodeACollectionAsync(fbRef))
										{
											if (!countFiledsFirebase.TryGetValue(fbRef, out int count))
												countFiledsFirebase[fbRef] = await firebaseService.AddCountFieldToCollectionAsync(fbRef);
											countFiledsFirebase[fbRef]++;
										}
										else
										{
											await firebaseService.ConvertFieldToCollectionAsync<string>(fbRef);
											countFiledsFirebase[fbRef] = 1;
										}

										int number = Math.Max(0, countFiledsFirebase[fbRef] - 1);
										await firebaseService.UpdateDataAsync<string>($"{fbRef}/{lastElement}-{number}", data);
										await firebaseService.UpdateDataAsync<string>($"{fbRef}/count", countFiledsFirebase[fbRef].ToString());
									}
									else
										await firebaseService.UpdateDataAsync<string>(fbRef, data);
								}
								else
									logger.Error($"Firebase reference cannot be is empty!");
							}
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
								if (rule != null)
								{
									if (!string.IsNullOrEmpty(rule.MQTT_topic))
									{
										string firebaseData = await firebaseService.GetDataAsync<string>(rule.FirebaseReference);
										if (!string.IsNullOrEmpty(firebaseData))
										{
											if (!subscriptionsFirebase.TryGetValue(rule.FirebaseReference, out string? oldValue) || oldValue != firebaseData)
											{
												logger.Info($"Message recived: Firebase path: [{rule.FirebaseReference}] Message: [{firebaseData}]");

												subscriptionsFirebase[rule.FirebaseReference] = firebaseData;
												mqttReciverClientFirebase.Publish(rule.MQTT_topic, firebaseData);
											}
										}
									}
									else
										logger.Error($"Topic path cannot be is empty!");
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
				if (!string.IsNullOrEmpty(p.ID_FIRESTORE) && !string.IsNullOrEmpty(p.PATH_FIRESTORE))
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

							if (rule != null && !string.IsNullOrEmpty(eMQTT.Payload))
							{
								if (!string.IsNullOrEmpty(rule.FirebaseReference))
								{
									string data = eMQTT.Payload;
									if (rule.Timestamp)
									{
										data = data.Split('|')[0];
										data += $"|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
									}

									FirestorePath fsPath = new FirestorePath(rule.FirebaseReference);
									if (rule.NewField)
									{
										FirestorePath fsPathNewField = new FirestorePath(rule.FirebaseReference).Shift();
										if (!fsPath.IsOdd()) //если четный значит док или коллекция
										{
											if (!countFiledsFirestore.TryGetValue(fsPathNewField.SourcePath, out int count))
												countFiledsFirestore[fsPathNewField.SourcePath] = await firestoreService.AddCountFieldToDocumentAsync(fsPathNewField);
											countFiledsFirestore[fsPathNewField.SourcePath]++;
										}
										else
										{
											string document = await firestoreService.ConvertFieldToCollectionAsync<string>(fsPath);
											fsPathNewField = new FirestorePath($"{fsPathNewField.ToString()}/{document}").Shift();
											rule.FirebaseReference = fsPathNewField.ToString();
											WriteInJSON(filePathFirestoreRules, ruleControlsFirestore.ToList()); //так как путь изменился нужно записать новый (перезапись файла fs-правил)

											countFiledsFirestore[rule.FirebaseReference] = 1;
										}

										int number = Math.Max(0, countFiledsFirestore[rule.FirebaseReference] - 1);
										var updatesNewField = new Dictionary<string, object>
										{
											{ $"{fsPathNewField.Document}-{number}", data }
										};
										await firestoreService.AddDataAsync(fsPathNewField, updatesNewField);
										var updatesCount = new Dictionary<string, object>
										{
											{ $"count", countFiledsFirestore[rule.FirebaseReference] }
										};
										await firestoreService.UpdateDataAsync(fsPathNewField, updatesCount);
									}
									else
									{
										var updates = new Dictionary<string, object>
										{
											{ fsPath.Field, data }
										};
										await firestoreService.UpdateDataAsync(fsPath, updates);
									}
								}
								else
									logger.Error($"Firebase reference cannot be is empty!");
							}
						};
					});

					//Firestore
					Task.Run(() =>
					{
						firestoreService.OnMessage += (senderFirestore, eFirestore) =>
						{
							if (eFirestore.Path != null && eFirestore.Data != null && !string.IsNullOrEmpty(eFirestore.Data.ToString()))
							{
								logger.Info($"Message recived: Firestore path: [{eFirestore.Path.SourcePath}] Message: [{eFirestore.Data.ToString()}]");

								List<RuleControl> rules;
								lock (ruleControlsFirestore)
								{
									rules = ruleControlsFirestore.Where(r => r.Direction == false).ToList(); // Создаём копию списка
								}
								RuleControl? rule = rules.FirstOrDefault(r => r.FirebaseReference == eFirestore.Path.SourcePath);

								if (rule != null)
								{
									if (!string.IsNullOrEmpty(rule.MQTT_topic))
									{
										mqttReciverClientFirestore.Publish(rule.MQTT_topic, eFirestore.Data.ToString()!);
									}
									else
										logger.Error($"Topic path cannot be is empty!");
								}
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
		private static void AddRuleFirebase(string FirebaseReference, string MQTT_topic, bool Direction, bool NewField, bool Timestamp)
		{
			// Создаем новый элемент управления
			RuleControl ruleControl = new RuleControl(FirebaseReference, MQTT_topic, Direction, NewField, Timestamp);
			// Добавляем его в список для управления
			ruleControlsFirebase.Add(ruleControl);

			if (mqttReciverClientFirebase != null && firebaseService != null)
			{
				if (ruleControl.Direction)
					mqttReciverClientFirebase.Subscribe(ruleControl.MQTT_topic);

				logger.Info($"Activate rule: [{ruleControl.FirebaseReference}] {(ruleControl.Direction ? "<" : ">")} [{ruleControl.MQTT_topic}].");
			}
		}

		//----------------------------------------------------- FIRESTORE RULES -----------------------------------------------------

		private static List<RuleControl> ruleControlsFirestore = new List<RuleControl>();
		private static void AddRuleFirestore(string FirebaseReference, string MQTT_topic, bool Direction, bool NewField, bool Timestamp)
		{
			// Создаем новый элемент управления
			RuleControl ruleControl = new RuleControl(FirebaseReference, MQTT_topic, Direction, NewField, Timestamp);
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

		private static void WriteInJSON(string path, List<RuleControl> ruleControls)
		{
			List<RuleUnit> ruleUnits = new List<RuleUnit>();
			foreach (RuleControl rule in ruleControls)
			{
				ruleUnits.Add(new RuleUnit(rule.FirebaseReference, rule.MQTT_topic, rule.Direction, rule.NewField, rule.Timestamp));
			}
			// Сериализуем список в JSON
			string json = JsonConvert.SerializeObject(ruleUnits, Formatting.Indented);
			// Записываем JSON в файл
			File.WriteAllText(path, json);
		}

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
