using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Orleans;
using Orleans.Runtime;

namespace Orleankka.Core
{
    using Cluster;

    /// <summary> 
    /// FOR INTERNAL USE ONLY!
    /// </summary>
    public abstract class ActorEndpoint : Grain, IRemindable
    {
        const string StickyReminderName = "##sticky##";

        readonly ActorType type;
        ActorRuntime runtime;
        internal IActorInvoker invoker;

        protected ActorEndpoint(string type)
        {
            this.type = ActorType.Registered(type);
        }

        public Task Autorun()
        {
            KeepAlive();

            return TaskDone.Done;
        }

        public Task<object> Receive(object message)
        {
            KeepAlive();

            return invoker.OnReceive(message);
        }

        public Task ReceiveVoid(object message) => Receive(message);

        async Task IRemindable.ReceiveReminder(string name, TickStatus status)
        {
            KeepAlive();

            if (name == StickyReminderName)
                return;

            await invoker.OnReminder(name);
        }

        public override async Task OnActivateAsync()
        {
            if (type.Sticky)
                await HandleStickyness();

            await Activate(ActorPath.Deserialize(IdentityOf(this)));
        }

        public override Task OnDeactivateAsync()
        {
            return runtime != null
                       ? invoker.OnDeactivate()
                       : base.OnDeactivateAsync();
        }

        Task Activate(ActorPath path)
        {
            runtime = new ActorRuntime(ClusterActorSystem.Current, this);
            invoker = type.Activate(path, runtime);
            return invoker.OnActivate();
        }

        async Task HandleStickyness()
        {
            var period = TimeSpan.FromMinutes(1);
            await RegisterOrUpdateReminder(StickyReminderName, period, period);
        }

        void KeepAlive() => type.KeepAlive(this);

        #region Internals

        internal new void DeactivateOnIdle()
        {
            base.DeactivateOnIdle();
        }

        internal new void DelayDeactivation(TimeSpan timeSpan)
        {
            base.DelayDeactivation(timeSpan);
        }

        internal new Task<IGrainReminder> GetReminder(string reminderName)
        {
            return base.GetReminder(reminderName);
        }

        internal new Task<List<IGrainReminder>> GetReminders()
        {
            return base.GetReminders();
        }

        internal new Task<IGrainReminder> RegisterOrUpdateReminder(string reminderName, TimeSpan dueTime, TimeSpan period)
        {
            return base.RegisterOrUpdateReminder(reminderName, dueTime, period);
        }

        internal new Task UnregisterReminder(IGrainReminder reminder)
        {
            return base.UnregisterReminder(reminder);
        }

        internal new IDisposable RegisterTimer(Func<object, Task> asyncCallback, object state, TimeSpan dueTime, TimeSpan period)
        {
            return base.RegisterTimer(asyncCallback, state, dueTime, period);
        }

        #endregion

        static string IdentityOf(IGrain grain)
        {
            return (grain as IGrainWithStringKey).GetPrimaryKeyString();
        }
    }
}