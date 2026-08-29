using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Infrastructure.SaveSystem
{
    public sealed class ReflectionSerializer
    {
        public string Serialize(object target)
        {
            var node = SerializeObject(target);
            return JsonUtility.ToJson(node, true);
        }

        public void DeserializeInto(string json, object target)
        {
            if (string.IsNullOrWhiteSpace(json))
                return;

            var node = JsonUtility.FromJson<SerializableNode>(json);

            if (node == null)
                return;

            ApplyObject(node, target);
        }

        private SerializableNode SerializeObject(object target)
        {
            var node = new SerializableNode();

            var fields = target.GetType()
                .GetFields(BindingFlags.Public | BindingFlags.Instance);

            foreach (var field in fields)
            {
                object value = field.GetValue(target);

                node.Fields.Add(new SerializableField
                {
                    Name = field.Name,
                    Value = SerializeValue(value)
                });
            }

            return node;
        }

        private SerializableValue SerializeValue(object value)
        {
            if (value == null)
                return new SerializableValue { Type = "null" };

            Type type = value.GetType();

            if (type == typeof(int))
                return SerializableValue.Primitive("int", value.ToString());

            if (type == typeof(float))
                return SerializableValue.Primitive(
                    "float",
                    ((float)value).ToString(System.Globalization.CultureInfo.InvariantCulture));

            if (type == typeof(double))
                return SerializableValue.Primitive(
                    "double",
                    ((double)value).ToString(System.Globalization.CultureInfo.InvariantCulture));

            if (type == typeof(bool))
                return SerializableValue.Primitive("bool", value.ToString());

            if (type == typeof(string))
                return SerializableValue.Primitive("string", (string)value);

            if (type.IsEnum)
                return SerializableValue.Primitive("enum", value.ToString());

            if (type == typeof(Vector2))
            {
                var v = (Vector2)value;

                return new SerializableValue
                {
                    Type = "Vector2",
                    X = v.x,
                    Y = v.y
                };
            }

            if (type == typeof(Vector3))
            {
                var v = (Vector3)value;

                return new SerializableValue
                {
                    Type = "Vector3",
                    X = v.x,
                    Y = v.y,
                    Z = v.z
                };
            }

            if (typeof(IList).IsAssignableFrom(type))
            {
                var list = (IList)value;
                var items = new List<SerializableValue>();

                foreach (var item in list)
                    items.Add(SerializeValue(item));

                return new SerializableValue
                {
                    Type = "List",
                    Items = items
                };
            }

            return new SerializableValue
            {
                Type = "Object",
                Object = SerializeObject(value)
            };
        }

        private void ApplyObject(SerializableNode node, object target)
        {
            var fields = target.GetType()
                .GetFields(BindingFlags.Public | BindingFlags.Instance);

            foreach (var field in fields)
            {
                SerializableField savedField = node.Fields.Find(f => f.Name == field.Name);

                if (savedField == null)
                    continue;

                object value = DeserializeValue(savedField.Value, field.FieldType);

                field.SetValue(target, value);
            }
        }

        private object DeserializeValue(SerializableValue value, Type targetType)
        {
            if (value == null || value.Type == "null")
                return null;

            if (targetType == typeof(int))
                return int.Parse(value.Raw);

            if (targetType == typeof(float))
                return float.Parse(
                    value.Raw,
                    System.Globalization.CultureInfo.InvariantCulture);

            if (targetType == typeof(double))
                return double.Parse(
                    value.Raw,
                    System.Globalization.CultureInfo.InvariantCulture);

            if (targetType == typeof(bool))
                return bool.Parse(value.Raw);

            if (targetType == typeof(string))
                return value.Raw;

            if (targetType.IsEnum)
                return Enum.Parse(targetType, value.Raw);

            if (targetType == typeof(Vector2))
                return new Vector2(value.X, value.Y);

            if (targetType == typeof(Vector3))
                return new Vector3(value.X, value.Y, value.Z);

            object instance = Activator.CreateInstance(targetType);

            ApplyObject(value.Object, instance);

            return instance;
        }

        [Serializable]
        private sealed class SerializableNode
        {
            public List<SerializableField> Fields = new();
        }

        [Serializable]
        private sealed class SerializableField
        {
            public string Name;
            public SerializableValue Value;
        }

        [Serializable]
        private sealed class SerializableValue
        {
            public string Type;
            public string Raw;

            public float X;
            public float Y;
            public float Z;

            public SerializableNode Object;
            public List<SerializableValue> Items;

            public static SerializableValue Primitive(string type, string raw)
            {
                return new SerializableValue
                {
                    Type = type,
                    Raw = raw
                };
            }
        }
    }
}