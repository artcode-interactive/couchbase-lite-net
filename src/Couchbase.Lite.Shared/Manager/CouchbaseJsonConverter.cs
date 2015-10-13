//
// CouchbaseJsonConverter.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2015 Couchbase, Inc All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

namespace Couchbase.Lite.Util
{
    internal sealed class CouchbaseJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return ToObject(JToken.Load(reader), objectType);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            JToken.FromObject(value).WriteTo(writer);
        }

        private static object ToObject(JToken token, Type objectType)
        {
            if (objectType.FullName.StartsWith("System.Collections.Generic.IList`1") ||
               objectType.FullName.StartsWith("System.Collections.Generic.IDictionary`2") || 
               objectType.FullName.StartsWith("System.Object")) {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        {
                            var keyType = objectType.IsGenericType ? objectType.GetGenericArguments()[0] : typeof(string);
                            var valueType = objectType.IsGenericType ? objectType.GetGenericArguments()[1] : typeof(object);
                            var container = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(keyType, valueType));
                            foreach (var child in token.Children<JProperty>()) {
                                container[child.Name] = ToObject(child.Value, valueType);
                            }

                            return container;
                        }
                    case JTokenType.Array:
                        {
                            var childType = objectType.IsGenericType ? objectType.GetGenericArguments()[0] : typeof(object);

                            var container = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(childType));
                            foreach (var child in token) {
                                container.Add(ToObject(child, childType));
                            }

                            return container;
                        }
                    default:
                        return ((JValue)token).Value;
                }
            }

            return token.ToObject(objectType);
        }
    }
}

