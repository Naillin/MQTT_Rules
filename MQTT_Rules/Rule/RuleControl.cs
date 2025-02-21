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
		private bool _newFiled = false;
		public bool NewField
		{
			get
			{
				return _newFiled;
			}
			set
			{
				_newFiled = value;
			}
		}
		private bool _timestamp = false;
		public bool Timestamp
		{
			get
			{
				return _timestamp;
			}
			set
			{
				_timestamp = value;
			}
		}

		public RuleControl(string FirebaseReference, string MQTT_topic, bool Direction, bool NewField, bool Timestamp)
		{
			this.FirebaseReference = FirebaseReference;
			this.MQTT_topic = MQTT_topic;
			this.Direction = Direction;
			this.NewField = NewField;
			this.Timestamp = Timestamp;
		}
	}
}
