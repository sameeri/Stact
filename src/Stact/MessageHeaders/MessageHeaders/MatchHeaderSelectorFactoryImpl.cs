﻿// Copyright 2010 Chris Patterson
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use 
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed 
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace Stact.MessageHeaders
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Linq.Expressions;
	using System.Reflection;
	using Magnum.Extensions;


	public abstract class MatchHeaderSelectorFactoryImpl :
		MatchHeaderSelectorFactory
	{
		readonly Type _bodyType;
		readonly Type _headerType;
		readonly bool _includeInherited;
		readonly bool _matches;
		readonly Type _messageType;
		readonly string _methodName;
		readonly Action<object, MatchHeaderCallback> _router;

		protected MatchHeaderSelectorFactoryImpl(Type messageType, Type headerType, string methodName,
		                                         bool includeInherited = false)
		{
			_messageType = messageType;
			_headerType = headerType;
			_methodName = methodName;
			_includeInherited = includeInherited;

			_matches = _messageType.IsGenericType && _messageType.Implements(_headerType);
			if (_matches)
			{
				_bodyType = _messageType.GetGenericTypeDeclarations(_headerType).Single();

				_router = GenerateRouteMethod(_bodyType);
			}
		}

		public bool CanMatch<TInput>(TInput input, out Action<object, MatchHeaderCallback> adapter)
		{
			adapter = null;

			if (!_matches)
				return false;

			if (!_messageType.Equals(typeof(TInput)))
				return false;

			if (_includeInherited)
				adapter = GenerateAdapterForInheritedTypes();
			else
				adapter = _router;
			return true;
		}

		public bool CanMatch<TContext, TInput>(TInput input,
		                                       out Action<TContext, object, MatchHeaderCallback<TContext>> adapter)
		{
			adapter = null;

			if (!_matches)
				return false;

			if (!_messageType.Equals(typeof(TInput)))
				return false;

			if (_includeInherited)
				adapter = GenerateContextAdapterForInheritedTypes<TContext>();
			else
				adapter = GenerateContextMethod<TContext>(_bodyType);
			return true;
		}

		IEnumerable<Type> GetInheritedTypes(Type type)
		{
			foreach (Type interfaceType	 in type.GetInterfaces())
				yield return interfaceType;

			Type baseType = type.BaseType;
			while (baseType != null && baseType != typeof(object))
			{
				yield return baseType;
				baseType = baseType.BaseType;
			}
		}

		Action<object, MatchHeaderCallback> GenerateAdapterForInheritedTypes()
		{
			Action<object, MatchHeaderCallback>[] adapters = Enumerable.Repeat(_router, 1)
				.Union(GetInheritedTypes(_bodyType).Select(GenerateRouteMethod))
				.ToArray();

			return (message, callback) =>
			{
				for (int i = 0; i < adapters.Length; i++)
					adapters[i](message, callback);
			};
		}

		Action<TContext, object, MatchHeaderCallback<TContext>> GenerateContextAdapterForInheritedTypes<TContext>()
		{
			Action<TContext, object, MatchHeaderCallback<TContext>>[] adapters =
				Enumerable.Repeat(GenerateContextMethod<TContext>(_bodyType), 1)
					.Union(GetInheritedTypes(_bodyType).Select(GenerateContextMethod<TContext>))
					.ToArray();

			return (context, message, callback) =>
			{
				for (int i = 0; i < adapters.Length; i++)
					adapters[i](context, message, callback);
			};
		}

		Action<object, MatchHeaderCallback> GenerateRouteMethod(Type bodyType)
		{
			ParameterExpression value = Expression.Parameter(typeof(object), "value");
			ParameterExpression output = Expression.Parameter(typeof(MatchHeaderCallback), "output");

			Type messageType = _headerType.MakeGenericType(bodyType);

			UnaryExpression castValue = Expression.TypeAs(value, messageType);

			MethodInfo method = typeof(MatchHeaderCallback)
				.GetMethod(_methodName)
				.MakeGenericMethod(bodyType);

			MethodCallExpression call = Expression.Call(output, method, castValue);

			Expression<Action<object, MatchHeaderCallback>> expression =
				Expression.Lambda<Action<object, MatchHeaderCallback>>(call, value, output);

			return expression.Compile();
		}

		Action<TContext, object, MatchHeaderCallback<TContext>> GenerateContextMethod<TContext>(Type bodyType)
		{
			ParameterExpression context = Expression.Parameter(typeof(TContext), "context");
			ParameterExpression value = Expression.Parameter(typeof(object), "value");
			ParameterExpression output = Expression.Parameter(typeof(MatchHeaderCallback<TContext>), "output");

			Type messageType = _headerType.MakeGenericType(bodyType);

			UnaryExpression castValue = Expression.TypeAs(value, messageType);

			MethodInfo method = typeof(MatchHeaderCallback<TContext>)
				.GetMethod(_methodName)
				.MakeGenericMethod(bodyType);

			MethodCallExpression call = Expression.Call(output, method, context, castValue);

			Expression<Action<TContext, object, MatchHeaderCallback<TContext>>> expression =
				Expression.Lambda<Action<TContext, object, MatchHeaderCallback<TContext>>>(call, context, value, output);

			return expression.Compile();
		}
	}
}