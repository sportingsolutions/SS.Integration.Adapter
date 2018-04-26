using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Actor.Internal;
using log4net;

namespace SS.Integration.Adapter.Actors.Strategy
{
    public class FailSupervisorStrategy : OneForOneStrategy
    {
        private static ILog _logger = LogManager.GetLogger(typeof(FailSupervisorStrategy).ToString());

        protected override Directive Handle(IActorRef child, Exception exception)
        {
            _logger.Warn($"{exception}");
            return base.Handle(child, exception);
        }

        [Obsolete]
        protected override void ProcessFailure(IActorContext context, bool restart, Exception cause, ChildRestartStats failedChildStats,
            IReadOnlyCollection<ChildRestartStats> allChildren)
        {
            _logger.Warn($"{cause}");
            base.ProcessFailure(context, restart, cause, failedChildStats, allChildren);
        }

        protected override void ProcessFailure(IActorContext context, bool restart, IActorRef child, Exception cause, ChildRestartStats stats,
            IReadOnlyCollection<ChildRestartStats> children)
        {
            _logger.Warn($"{cause}");
            base.ProcessFailure(context, restart, child, cause, stats, children);
        }

        protected override void LogFailure(IActorContext context, IActorRef child, Exception cause, Directive directive)
        {
            _logger.Warn($"{cause}");
            base.LogFailure(context, child, cause, directive);
        }
    }
}
