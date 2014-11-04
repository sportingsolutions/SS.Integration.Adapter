using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using log4net.Util;
using SS.Integration.Adapter.Model.Enums;

namespace SS.Integration.Adapter.Diagnostics.Model
{
    public class FixtureOverview
    {
        private IDictionary<string, PropertyChanged> _changes;
        private bool? _isStreaming;

        public FixtureOverview()
        {
            _changes = new Dictionary<string, PropertyChanged>();
            TimeStamp = DateTime.UtcNow;
        }


        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime TimeStamp { get; private set; }
        public MatchStatus? MatchStatus { get; set; }
        public int? Sequence { get; set; }


        public bool? IsStreaming
        {
            get { return _isStreaming; }
            set
            {
                OnChanged(_isStreaming,value);
                _isStreaming = value;
            } 
        }

        public bool? IsDeleted { get; set; }
        public bool? IsErrored { get; set; }
        public bool? IsOver { get; set; }

        private void OnChanged(bool? oldValue, bool? newValue, [CallerMemberName] string callerName = null)
        {
            var oldValueString = oldValue.HasValue ? oldValue.Value.ToString() : null;
            OnChanged(oldValueString,newValue.ToString(),callerName);
        }

        private void OnChanged(string oldValue,string newValue, [CallerMemberName] string callerName = null)
        {
            var propertyChanged = new PropertyChanged
            {
                CurrentValue = newValue,
                PreviousValue = oldValue,
                ItemName = callerName
            };

            propertyChanged.SetTimeStamp();

            _changes[callerName] = propertyChanged;
        }

        public IEnumerable<PropertyChanged> GetChanges()
        {
            _changes.ToList();
        }

        public void Merge(FixtureOverview other)
        {
            //other._changes 

        }
    }
}
