using Ark.Tools.Sql;

using SimpleInjector;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Outbox.SqlServer
{
    public class ServiceFactory<TKey>
    {
        private readonly Container _container;
        private readonly IDictionary<TKey, InstanceProducer> _instanceProducers =
            new Dictionary<TKey, InstanceProducer>();

        //constructor
        public ServiceFactory(Container container)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
        }

        public void Register(TKey key, Type serviceType, Type implementationType,
            Lifestyle? lifestyle = null)
        {
            var producer = (lifestyle ?? _container.Options.DefaultLifestyle).CreateProducer(serviceType, implementationType, _container);
            _instanceProducers.Add(key, producer);
        }

        public async Task<ISqlContextAsync<TKey>> Create(TKey key, IsolationLevel isolation = IsolationLevel.ReadCommitted, CancellationToken ctk = default)
        {

            if (_instanceProducers.ContainsKey(key))
            {
                var instance = (ISqlContextAsync<TKey>)_instanceProducers[key].GetInstance();

                await instance.Create(isolation, ctk);

                return instance;
            }
            else
            {
                throw new ArgumentException("Not Exist");
            }
        }
    }
}
