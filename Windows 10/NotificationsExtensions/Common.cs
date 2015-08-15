﻿// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO
// THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
// PARTICULAR PURPOSE.
//
// Copyright (c) Microsoft Corporation. All rights reserved

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using System.Linq;
using System.Collections;
#if !WINRT_NOT_PRESENT
using Windows.Data.Xml.Dom;
#endif

namespace NotificationsExtensions
{
    internal sealed class LimitedList<T> : IList<T>
    {
        private List<T> _list;
        public int Limit { get; private set; }

        public LimitedList(int limit)
        {
            _list = new List<T>(limit);

            Limit = limit;
        }

        public T this[int index]
        {
            get
            {
                return _list[index];
            }

            set
            {
                _list[index] = value;
            }
        }

        public int Count
        {
            get
            {
                return _list.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public void Add(T item)
        {
            if (_list.Count >= Limit)
                throw new Exception("This list is limited to " + Limit + " items. You cannot add more items.");

            _list.Add(item);
        }

        public void Clear()
        {
            _list.Clear();
        }

        public bool Contains(T item)
        {
            return _list.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _list.CopyTo(array, arrayIndex);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        public int IndexOf(T item)
        {
            return _list.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            _list.Insert(index, item);
        }

        public bool Remove(T item)
        {
            return _list.Remove(item);
        }

        public void RemoveAt(int index)
        {
            _list.RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    internal sealed class NotificationXmlAttributeAttribute : Attribute
    {
        public string Name { get; private set; }

        public object DefaultValue { get; private set; }

        public NotificationXmlAttributeAttribute(string name, object defaultValue = null)
        {
            Name = name;
            DefaultValue = defaultValue;
        }
    }

    internal sealed class NotificationXmlElementAttribute : Attribute
    {
        public string Name { get; private set; }

        public NotificationXmlElementAttribute(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException("name cannot be null or whitespace");

            Name = name;
        }
    }

    /// <summary>
    /// This attribute should be specified at most one time on an Element class. The property's value will be written as a string in the element's body.
    /// </summary>
    internal sealed class NotificationXmlContentAttribute : Attribute
    {

    }

    internal sealed class EnumStringAttribute : Attribute
    {
        public string String { get; private set; }

        public EnumStringAttribute(string s)
        {
            if (s == null)
                throw new ArgumentNullException("string cannot be null");

            String = s;
        }

        public override string ToString()
        {
            return String;
        }
    }

    internal static class XmlWriterHelper
    {
        public static void Write(XmlWriter writer, object element)
        {
            NotificationXmlElementAttribute elAttr = GetElementAttribute(element.GetType());

            // If it isn't an element attribute, don't write anything
            if (elAttr == null)
                return;

            writer.WriteStartElement(elAttr.Name);



            IEnumerable<PropertyInfo> properties = GetProperties(element.GetType());

            List<object> elements = new List<object>();
            object content = null;

            // Write the attributes first
            foreach (PropertyInfo p in properties)
            {
                IEnumerable<Attribute> attributes = GetCustomAttributes(p);

                NotificationXmlAttributeAttribute attr = attributes.OfType<NotificationXmlAttributeAttribute>().FirstOrDefault();

                object propertyValue = GetPropertyValue(p, element);

                // If it's an attribute
                if (attr != null)
                {
                    object defaultValue = attr.DefaultValue;

                    // If the value is not the default value (and it's not null) we'll write it
                    if (!object.Equals(propertyValue, defaultValue) && propertyValue != null)
                        writer.WriteAttributeString(attr.Name, PropertyValueToString(propertyValue));
                }

                // If it's a content attribute
                else if (attributes.OfType<NotificationXmlContentAttribute>().Any())
                {
                    content = propertyValue;
                }

                // Otherwise it's an element or collection of elements
                else
                {
                    if (propertyValue != null)
                        elements.Add(propertyValue);
                }
            }

            // Then write children
            foreach (object el in elements)
            {
                // If it's a collection of children
                if (el is IEnumerable)
                {
                    foreach (object child in (el as IEnumerable))
                        Write(writer, child);

                    continue;
                }

                // Otherwise just write the single element
                Write(writer, el);
            }

            // Then write any content if there is content
            if (content != null)
            {
                string contentString = content.ToString();
                if (!string.IsNullOrWhiteSpace(contentString))
                    writer.WriteString(contentString);
            }




            writer.WriteEndElement();
        }

        private static object GetPropertyValue(PropertyInfo propertyInfo, object obj)
        {
#if WINRT_NOT_PRESENT
            return propertyInfo.GetValue(obj, null);
#else
            return propertyInfo.GetValue(obj);
#endif
        }

        private static string PropertyValueToString(object propertyValue)
        {
            Type type = propertyValue.GetType();

            if (IsEnum(type))
            {
                EnumStringAttribute enumStringAttr = GetEnumStringAttribute(propertyValue as Enum);

                if (enumStringAttr != null)
                    return enumStringAttr.String;
            }

            return propertyValue.ToString();
        }

        private static EnumStringAttribute GetEnumStringAttribute(Enum enumValue)
        {
#if WINRT_NOT_PRESENT
            MemberInfo[] memberInfo = enumValue.GetType().GetMember(enumValue.ToString());

            if (memberInfo != null && memberInfo.Length > 0)
            {
                object[] attrs = memberInfo[0].GetCustomAttributes(typeof(EnumStringAttribute), false);

                if (attrs != null && attrs.Length > 0)
                    return attrs[0] as EnumStringAttribute;
            }

            return null;
#else
            return enumValue.GetType().GetTypeInfo().GetDeclaredField(enumValue.ToString()).GetCustomAttribute<EnumStringAttribute>();
#endif
        }

        private static bool IsEnum(Type type)
        {
#if WINRT_NOT_PRESENT
            return type.IsEnum;
#else
            return type.GetTypeInfo().IsEnum;
#endif
        }

        private static IEnumerable<PropertyInfo> GetProperties(Type type)
        {
#if WINRT_NOT_PRESENT
            return type.GetProperties();
#else
            return type.GetTypeInfo().DeclaredProperties;
#endif
        }

        private static NotificationXmlElementAttribute GetElementAttribute(Type type)
        {
            return GetCustomAttributes(type).OfType<NotificationXmlElementAttribute>().FirstOrDefault();
        }

        private static IEnumerable<Attribute> GetCustomAttributes(Type type)
        {
#if WINRT_NOT_PRESENT
            return type.GetCustomAttributes(true).OfType<Attribute>();
#else
            return type.GetTypeInfo().GetCustomAttributes();
#endif
        }

        private static IEnumerable<Attribute> GetCustomAttributes(PropertyInfo propertyInfo)
        {
#if WINRT_NOT_PRESENT
            return propertyInfo.GetCustomAttributes(true).OfType<Attribute>();
#else
            return propertyInfo.GetCustomAttributes();
#endif
        }
    }

    internal static class ConversionHelper
    {
        internal static object ConvertToElement(object obj)
        {
            MethodInfo convertToElement = GetMethod(obj.GetType(), "ConvertToElement");

            if (convertToElement == null)
                throw new NotImplementedException("Object must have ConvertToElement() method");

            if (convertToElement.ReturnType == typeof(void))
                throw new NotImplementedException("ConvertToElement() must return an object");

            return convertToElement.Invoke(obj, null);
        }

        internal static MethodInfo GetMethod(Type type, string name)
        {
            return type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);

            //MethodInfo[] methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance);

            //return methods.FirstOrDefault(i => i.Name.Equals(name));
        }
    }

    public interface IElementWithDescendants
    {
        IEnumerable<object> Descendants();
    }



    

    internal static class Util
    {
        public const int NOTIFICATION_CONTENT_VERSION = 1;

        public static string HttpEncode(string value)
        {
            return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
        }
    }

    /// <summary>
    /// Exception returned when invalid notification content is provided.
    /// </summary>
    internal sealed class NotificationContentValidationException : Exception
    {
        public NotificationContentValidationException(string message)
            : base(message)
        {
        }
    }
}