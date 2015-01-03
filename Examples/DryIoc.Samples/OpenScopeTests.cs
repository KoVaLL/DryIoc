﻿using System;
using System.Linq;
using NUnit.Framework;

// ReSharper disable MemberHidesStaticFromOuterClass

namespace DryIoc.Samples
{
    [TestFixture]
    class OpenScopeTests
    {
        [Test]
        public void Session_example_of_scope_usage()
        {
            var container = new Container();
            container.Register<ISessionFactory, TestSessionFactory>();
            container.RegisterDelegate(r => r.Resolve<ISessionFactory>().OpenSession(), Reuse.InCurrentScope);

            ISession scopeOneSession;
            using (var scoped = container.OpenScope())
            {
                scopeOneSession = scoped.Resolve<ISession>();
                Assert.AreSame(scopeOneSession, scoped.Resolve<ISession>());
            }

            using (var scoped = container.OpenScope())
            {
                var scopeTwoSession = scoped.Resolve<ISession>();
                Assert.AreNotSame(scopeOneSession, scopeTwoSession);
                Assert.AreSame(scopeTwoSession, scoped.Resolve<ISession>());
            }
        }

        [Test]
        public void Session_example_of_scope_usage_when_factory_is_not_resolution_root()
        {
            var container = new Container();
            container.Register<SessionClient>();
            container.Register<ISessionFactory, TestSessionFactory>();
            container.RegisterDelegate(r => r.Resolve<ISessionFactory>().OpenSession(), Reuse.InCurrentScope);

            SessionClient client;
            using (var scoped = container.OpenScope())
            {
                client = scoped.Resolve<SessionClient>();
                Assert.AreSame(client.Session, scoped.Resolve<SessionClient>().Session);
            }

            using (var scoped = container.OpenScope())
            {
                var clientScope2 = scoped.Resolve<SessionClient>();
                Assert.AreNotSame(client.Session, clientScope2.Session);
                Assert.AreSame(clientScope2.Session, scoped.Resolve<SessionClient>().Session);
            }
        }

        [Test]
        public void Session_example_of_scope_usage_using_factory_method()
        {
            var container = new Container();
            container.Register<ISessionFactory, TestSessionFactory>();

            container.Register<ISession>(Reuse.InCurrentScope,
                with: FactoryMethod.Of(r => r.Resolve<ISessionFactory>(), f => f.OpenSession()));

            ISession scopeOneSession;
            using (var scoped = container.OpenScope())
            {
                scopeOneSession = scoped.Resolve<ISession>();
                Assert.AreSame(scopeOneSession, scoped.Resolve<ISession>());
            }

            using (var scoped = container.OpenScope())
            {
                var scopeTwoSession = scoped.Resolve<ISession>();
                Assert.AreNotSame(scopeOneSession, scopeTwoSession);
                Assert.AreSame(scopeTwoSession, scoped.Resolve<ISession>());
            }
        }

        #region Session example CUT

        internal interface ISession {}

        private class TestSession : ISession {}

        internal interface ISessionFactory
        {
            ISession OpenSession();
        }

        internal class TestSessionFactory : ISessionFactory
        {
            public ISession OpenSession()
            {
                return new TestSession();
            }
        }

        internal class SessionClient
        {
            public ISession Session { get; private set; }

            public SessionClient(ISession session)
            {
                Session = session;
            }
        }

        #endregion

        [Test]
        public void Can_override_registrations_in_open_scope()
        {
            var container = new Container();
            var scopeName = "blah";

            // two client versions: root and scoped
            container.Register<IClient, Client>();
            container.Register<IClient, ClientScoped>(named: scopeName);
            
            // uses
            container.Register<IServ, Serv>(Reuse.Singleton);
            
            // two dependency versions:
            container.Register<IDep, Dep>();
            container.Register<IDep, DepScoped>(named: scopeName);

            var client = container.Resolve<IClient>();
            
            Assert.That(client, Is.InstanceOf<Client>());
            Assert.That(client.Dep, Is.InstanceOf<Dep>());
            Assert.That(client.Serv, Is.InstanceOf<Serv>());

            using (var scoped = container.OpenScope(scopeName, 
                rules => rules.WithFactorySelector(MapDefaultToKey(scopeName))))
            {
                var scopedClient = scoped.Resolve<IClient>(scopeName);

                Assert.That(scopedClient, Is.InstanceOf<ClientScoped>());
                Assert.That(scopedClient.Dep, Is.InstanceOf<DepScoped>());
                Assert.That(scopedClient.Serv, Is.InstanceOf<Serv>());
            }

            client = container.Resolve<IClient>();
            Assert.That(client, Is.InstanceOf<Client>());
        }

        private static Rules.FactorySelectorRule MapDefaultToKey(object key)
        {
            return (t, k, fs) => k == null
                ? fs.FirstOrDefault(f => f.Key.Equals(key)).Value
                ?? fs.FirstOrDefault(f => f.Key.Equals(null)).Value
                : fs.FirstOrDefault(f => f.Key.Equals(k)).Value;
    }

        [Test]
        public void Services_should_be_different_in_different_scopes()
        {
            var container = new Container();
            container.Register<IndependentService>(Shared.InCurrentScope);

            var scope = container.OpenScope();
            var first = scope.Resolve<IndependentService>();
            scope.Dispose();

            scope = container.OpenScope();
            var second = scope.Resolve<IndependentService>();
            scope.Dispose();

            Assert.That(second, Is.Not.SameAs(first));
        }

        [Test]
        public void Factory_should_return_different_service_when_called_in_different_scopes()
        {
            var container = new Container();

            container.Register<IService, IndependentService>(Reuse.InCurrentScope);
            container.Register<ServiceWithFuncConstructorDependency>(Reuse.Singleton);

            var service = container.Resolve<ServiceWithFuncConstructorDependency>();

            var scope = container.OpenScope();
            var first = service.GetScopedService();
            scope.Dispose();

            scope = container.OpenScope();
            var second = service.GetScopedService();
            scope.Dispose();

            Assert.That(second, Is.Not.SameAs(first));
        }

        internal class IndependentService : IService { }

        internal class ServiceWithFuncConstructorDependency
        {
            public Func<IService> GetScopedService { get; private set; }

            public ServiceWithFuncConstructorDependency(Func<IService> getScopedService)
            {
                GetScopedService = getScopedService;
            }
        }

        internal interface IDep { }
        internal interface IServ { }
        internal interface IClient
        {
            IDep Dep { get; }
            IServ Serv { get; }
        }

        internal class Client : IClient
        {
            public IDep Dep { get; private set; }
            public IServ Serv { get; private set; }

            public Client(IDep dep, IServ serv)
            {
                Dep = dep;
                Serv = serv;
            }
        }

        internal class ClientScoped : IClient
        {
            public IDep Dep { get; private set; }
            public IServ Serv { get; private set; }

            public ClientScoped(IDep dep, IServ serv)
            {
                Dep = dep;
                Serv = serv;
            }
        }

        internal class Dep : IDep { }
        internal class DepScoped : IDep { }
        internal class Serv : IServ { }
    }
}