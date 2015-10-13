//
// CouchbaseKeyEqualityComparer.cs
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
using System.Collections.Generic;
using System.Linq;

namespace Couchbase.Lite.Store
{
    internal sealed class CouchbaseKeyEqualityComparer : IEqualityComparer<object>
    {
        public bool Equals(object a, object b)
        {
            var enumA = a as IEnumerable<object>;
            var enumB = b as IEnumerable<object>;

            if (enumA != null && enumB != null) {
                return enumA.SequenceEqual(enumB, this);
            }

            if (enumA != null || enumB != null) {
                return false;
            }

            if (a == null && b != null) {
                return false;
            }

            return a.Equals(b);
        }

        public int GetHashCode(object obj)
        {
            var enumA = obj as IEnumerable<object>;
            if (enumA == null) {
                return obj.GetHashCode();
            }

            int hash = 17;
            foreach (var element in enumA) {
                hash = hash * 31 + GetHashCode(element);
            }

            return hash;
        }
        
    }
}

