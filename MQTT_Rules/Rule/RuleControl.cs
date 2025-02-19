using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQTT_Rules.Rule
{
	internal class RuleControl
	{
		private string _firebaseReference = string.Empty;
		public string FirebaseReference
		{
			get
			{
				return _firebaseReference;
			}
			set
			{
				_firebaseReference = value;
			}
		}
		private string _MQTT_topic = string.Empty;
		public string MQTT_topic
		{
			get
			{
				return _MQTT_topic;
			}
			set
			{
				_MQTT_topic = value;
			}
		}
		private bool _direction = false;
		public bool Direction
		{
			get
			{
				return _direction;
			}
			set
			{
				_direction = value;
			}
		}

		public RuleControl(string FirebaseReference, string MQTT_topic, bool Direction)
		{
			this.FirebaseReference = FirebaseReference;
			this.MQTT_topic = MQTT_topic;
			this.Direction = Direction;
		}
	}
}
