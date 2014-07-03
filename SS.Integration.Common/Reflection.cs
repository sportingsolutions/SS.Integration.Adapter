//Copyright 2014 Spin Services Limited

//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at

//    http://www.apache.org/licenses/LICENSE-2.0

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

// License is located at: http://www.yoda.arachsys.com/csharp/miscutil/licence.txt
namespace SS.Integration.Common
{
    /// <summary>
    /// Generic class which copies to its target type from a source
    /// type specified in the Copy method. The types are specified
    /// separately to take advantage of type inference on generic
    /// method arguments.
    /// </summary>
    public class Reflection
    {
        public class IgnoreAttribute : Attribute { }

        public static class PropertyCopy<TTarget> where TTarget : class, new()
        {
            /// <summary>
            /// Copies all readable properties from the source to a new instance
            /// of TTarget.
            /// </summary>
            public static TTarget CopyFrom<TSource>(TSource source) where TSource : class
            {
                return PropertyCopier<TSource>.Copy(source);
            }

            /// <summary>
            /// Static class to efficiently store the compiled delegate which can
            /// do the copying. We need a bit of work to ensure that exceptions are
            /// appropriately propagated, as the exception is generated at type initialization
            /// time, but we wish it to be thrown as an ArgumentException.
            /// </summary>
            private static class PropertyCopier<TSource> where TSource : class
            {
                private static readonly Func<TSource, TTarget> copier;
                private static readonly Exception initializationException;

                internal static TTarget Copy(TSource source, bool ignoreReadOnly = false)
                {
                    if (initializationException != null)
                    {
                        throw initializationException;
                    }
                    if (source == null)
                    {
                        throw new ArgumentNullException("source");
                    }
                    return copier(source);
                }

                static PropertyCopier()
                {
                    try
                    {
                        copier = BuildCopier();
                        initializationException = null;
                    }
                    catch (Exception e)
                    {
                        copier = null;
                        initializationException = e;
                    }
                }

                private static Func<TSource, TTarget> BuildCopier()
                {
                    ParameterExpression sourceParameter = Expression.Parameter(typeof(TSource), "source");

                    var bindings = new List<MemberBinding>();
                    foreach (PropertyInfo sourceProperty in typeof(TSource).GetProperties())
                    {
                        
                        if (!sourceProperty.CanRead)
                        {
                            continue;
                        }

                        if (sourceProperty.GetCustomAttribute<IgnoreAttribute>() != null)
                            continue;

                        PropertyInfo targetProperty = typeof(TTarget).GetProperty(sourceProperty.Name);
                        if (targetProperty == null)
                        {
                            throw new ArgumentException("Property " + sourceProperty.Name +
                                                        " is not present and accessible in " + typeof(TTarget).FullName);
                        }

                        if (!targetProperty.CanWrite && !sourceProperty.CanWrite)
                            continue;

                        if (!targetProperty.CanWrite)
                        {
                            throw new ArgumentException("Property " + sourceProperty.Name + " is not writable in " +
                                                        typeof(TTarget).FullName);
                        }

                        if (!targetProperty.PropertyType.IsAssignableFrom(sourceProperty.PropertyType))
                        {
                            throw new ArgumentException("Property " + sourceProperty.Name +
                                                        " has an incompatible type in " + typeof(TTarget).FullName);
                        }
                        bindings.Add(Expression.Bind(targetProperty,
                                                     Expression.Property(sourceParameter, sourceProperty)));
                    }

                    Expression initializer = Expression.MemberInit(Expression.New(typeof(TTarget)), bindings);
                    return Expression.Lambda<Func<TSource, TTarget>>(initializer, sourceParameter).Compile();
                }

            }

        }
    }
}
