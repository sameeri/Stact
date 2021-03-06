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
namespace Stact.Configuration.Internal
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using Builders;
	using Magnum.Extensions;
	using Magnum.Reflection;


	public class PropertyChannelsConfiguratorImpl<T> :
		PropertyChannelsConfigurator<T>,
		ConnectionBuilderConfigurator
		where T : class
	{
		T _instance;
		List<PropertyChannelConfigurator<T>> _propertyBinders;

		public void ValidateConfiguration()
		{
			if (_instance == null)
				throw new ChannelConfigurationException("No instance was provided for " + typeof(T).ToShortTypeName());

			GetChannelBinders();
		}

		public void Configure(ConnectionBuilder builder)
		{
			_propertyBinders.Each(x => x.Configure(builder, _instance));
		}

		public PropertyChannelsConfigurator<T> UsingInstance(T instance)
		{
			_instance = instance;

			return this;
		}

		void GetChannelBinders()
		{
			_propertyBinders = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(x => x.PropertyType.Implements<Channel>())
				.Select(property =>
					{
						Type inputType = property.PropertyType.GetGenericTypeDeclarations(typeof(Channel<>)).Single();

						Type configuratorType = typeof(PropertyChannelConfiguratorImpl<,>).MakeGenericType(typeof(T), inputType);

						return (PropertyChannelConfigurator<T>)FastActivator.Create(configuratorType, new object[] {property});
					})
				.ToList();
		}
	}
}