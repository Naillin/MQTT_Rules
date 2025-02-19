using IniParser.Model;
using IniParser;
using NLog;
using MQTT_Rules.Rule;
using Newtonsoft.Json;
using MQTT_Rules.FirebaseTools;

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

			loadRules(filePathFirebaseRules, AddRuleFirebase);
			loadRules(filePathFirestoreRules, AddRuleFirestore);
		}

		private void loadRules(string path, Action<string, string, bool> AddRule)
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

		private FirebaseService firebaseService = new FirebaseService("", "");
		private MqttBrokerClient mqttReciverClientFirebase = new MqttBrokerClient("", 0, "", "");
		private Dictionary<string, string> subscriptionsFirebase = new Dictionary<string, string>();

		private FirestoreService firestoreService = new FirestoreService("", "");
		private MqttBrokerClient mqttReciverClientFirestore = new MqttBrokerClient("", 0, "", "");
		static async Task Main(string[] args)
		{
			Program p = new Program();
			try
			{
				p.initConfig();

				//MQTTReciverFirebase
				await Task.Run(() =>
				{
					p.mqttReciverClientFirebase = new MqttBrokerClient(p.ADDRESS, p.PORT, p.LOGIN, p.PASSWORD);
					p.mqttReciverClientFirebase.Connect();
					logger.Info("MQTT Reciver for Firebase is ready");
					p.mqttReciverClientFirebase.MessageReceived += async (senderMQTT, eMQTT) =>
					{
						logger.Info($"Message recived: Topic: [{eMQTT.Topic}] Message: [{eMQTT.Payload}]");

						RuleControl? rule;
						lock (p.ruleControlsFirebase)
						{
							List<RuleControl> rules = p.ruleControlsFirebase.Where(r => r.Direction == true).ToList();
							rule = rules.FirstOrDefault(r => r.MQTT_topic == eMQTT.Topic);
						}

						if (rule != null && eMQTT.Payload != null)
							await p.firebaseService.UpdateDataAsync<string>(rule.FirebaseReference, eMQTT.Payload);
					};
				});

				//Firebase
				p.firebaseService = new FirebaseService(p.URL_FIREBASE, p.SECRET_FIREBASE);
				await Task.Run(async () =>
				{
					while (true)
					{
						List<RuleControl> rules;
						lock (p.ruleControlsFirebase)
						{
							rules = p.ruleControlsFirebase.Where(r => r.Direction == false).ToList(); // Создаём копию списка
						}
						foreach (RuleControl rule in rules)
						{
							string firebaseData = await p.firebaseService.GetDataAsync<string>(rule.FirebaseReference);
							if (!p.subscriptionsFirebase.TryGetValue(rule.FirebaseReference, out string? oldValue) || oldValue != firebaseData)
							{
								logger.Info($"Message recived: Firebase path: [{rule.FirebaseReference}] Message: [{firebaseData}]");

								p.subscriptionsFirebase[rule.FirebaseReference] = firebaseData;
								p.mqttReciverClientFirebase.Publish(rule.MQTT_topic, firebaseData);
							}
						}

						await Task.Delay(3000);
					}
				});

				//MQTTReciverFirestore
				await Task.Run(() =>
				{
					p.mqttReciverClientFirestore = new MqttBrokerClient(p.ADDRESS, p.PORT, p.LOGIN, p.PASSWORD);
					p.mqttReciverClientFirestore.Connect();
					logger.Info("MQTT Reciver for Firestore is ready");
					p.mqttReciverClientFirestore.MessageReceived += async (senderMQTT, eMQTT) =>
					{
						logger.Info($"Message recived: Topic: [{eMQTT.Topic}] Message: [{eMQTT.Payload}]");

						RuleControl? rule;
						lock (p.ruleControlsFirebase)
						{
							List<RuleControl> rules = p.ruleControlsFirestore.Where(r => r.Direction == true).ToList();
							rule = rules.FirstOrDefault(r => r.MQTT_topic == eMQTT.Topic);
						}

						if (rule != null && eMQTT.Payload != null)
						{
							FirestorePath path = new FirestorePath(rule.FirebaseReference);
							var updates = new Dictionary<string, object>
							{
								{ path.Field, eMQTT.Payload }
							};
							await p.firestoreService.UpdateDataAsync(path, updates);
						}
					};
				});

				////Firestore
				//if (!string.IsNullOrEmpty(ID_FIRESTORE) && !string.IsNullOrEmpty(PATH_FIRESTORE))
				//{
				//	firestoreService = new FirestoreService(ID_FIRESTORE, PATH_FIRESTORE);
				//	Task.Run(async () =>
				//	{
				//		while (true && !disconnect)
				//		{
				//			if (switcherFirestore)
				//			{
				//				List<RuleControl> rules;
				//				lock (ruleControlsFirestore)
				//				{
				//					rules = ruleControlsFirestore.Where(r => r.Direction == false).ToList(); // Создаём копию списка
				//				}
				//				foreach (RuleControl rule in rules)
				//				{
				//					FirestorePath path = new FirestorePath(rule.FirebaseReference);
				//					string firestoreData = await firestoreService.GetFieldAsync<string>(path);
				//					if (!subscriptionsFirestore.TryGetValue(rule.FirebaseReference, out string oldValue) || oldValue != firestoreData)
				//					{
				//						string postString = Properties.Resources.notification_string + $"Firestore path: [{rule.FirebaseReference}] Message: [{firestoreData}]";
				//						logger.Info(postString);

				//						subscriptionsFirestore[rule.FirebaseReference] = firestoreData;
				//						mqttBrokerClient.Publish(rule.MQTT_topic, firestoreData);
				//					}
				//				}

				//				await Task.Delay(3000);
				//			}
				//		}
				//	});
				//}

				//Firestore
				p.firestoreService = new FirestoreService(p.ID_FIRESTORE, p.PATH_FIRESTORE);
				await Task.Run(() =>
				{
					p.firestoreService.OnMessage += (senderFirestore, eFirestore) =>
					{
						logger.Info($"Message recived: Firebase path: [{eFirestore.Path.SourcePath}] Message: [{eFirestore.Data.ToString()}]");

						List<RuleControl> rules;
						lock (p.ruleControlsFirestore)
						{
							rules = p.ruleControlsFirestore.Where(r => r.Direction == false).ToList(); // Создаём копию списка
						}
						RuleControl? rule = rules.FirstOrDefault(r => r.FirebaseReference == eFirestore.Path.SourcePath);

						if (rule != null && eFirestore.Data != null)
							p.mqttReciverClientFirestore.Publish(rule.MQTT_topic, eFirestore.Data.ToString()!);

					};
				});
			}
			catch (Exception ex)
			{
				logger.Error($"Error: {ex.Message}");
				Environment.Exit(0);
			}
		}

		//----------------------------------------------------- FIREBASE RULES -----------------------------------------------------

		private List<RuleControl> ruleControlsFirebase = new List<RuleControl>();
		private void AddRuleFirebase(string FirebaseReference, string MQTT_topic, bool Direction)
		{
			// Создаем новый элемент управления
			RuleControl ruleControl = new RuleControl(FirebaseReference, MQTT_topic, Direction);
			// Добавляем его в список для управления
			ruleControlsFirebase.Add(ruleControl);
		}

		//----------------------------------------------------- FIRESTORE RULES -----------------------------------------------------

		private List<RuleControl> ruleControlsFirestore = new List<RuleControl>();
		private void AddRuleFirestore(string FirestoreReference, string MQTT_topic, bool Direction)
		{
			// Создаем новый элемент управления
			RuleControl ruleControl = new RuleControl(FirestoreReference, MQTT_topic, Direction);
			// Добавляем его в список для управления
			ruleControlsFirestore.Add(ruleControl);
		}
	}
}
