using System.Reflection;

namespace DynamicProxy
{
    public class DynamicEvent
    {
        private readonly DynamicMethod _addMethod;
        private readonly DynamicMethod _removeMethod;

        public DynamicEvent(EventInfo eventInfo, object instance)
        {
            _addMethod = new DynamicMethod(eventInfo.GetAddMethod(true), instance);
            _removeMethod = new DynamicMethod(eventInfo.GetRemoveMethod(true), instance);
        }

        public void Add(object value)
        {
            _addMethod.Invoke(value.ToArrayItem<object>());
        }

        public void Remove(object value)
        {
            _removeMethod.Invoke(value.ToArrayItem<object>());
        }
    }
}