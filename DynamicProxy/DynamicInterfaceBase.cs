using System;

namespace DynamicProxy
{
    public abstract class DynamicInterfaceBase
    {
        private readonly DynamicProperty _WritableProperty;
        private readonly DynamicProperty _ReadOnlyProperty;
        private readonly DynamicMethod _GetStringValue;
        private readonly DynamicMethod _GetInt;
        private readonly DynamicIndex _indexer;

        public string this[int index]
        {
            get { return (string)_indexer.Get(index); }
            set { _indexer.Set(new object[] { index }, value); }
        }

        public string this[string index, int anotherIndex]
        {
            get { return (string)_indexer.Get(index, anotherIndex); }
            set { _indexer.Set(new object[] { index, anotherIndex }, value); }
        }


        protected string WritableProperty
        {
            get { return (string)_WritableProperty.Get(); }
            set { _WritableProperty.Set(value); }
        }

        protected string ReadOnlyPoperty
        {
            get
            {
                return (string)_ReadOnlyProperty.Get();
            }
        }

        protected string GetStringValue(object input, object anotherInput)
        {
            return (string)_GetStringValue.Invoke(new[] { input, anotherInput });
        }

        public int GetInt(int input)
        {
            return (int)_GetInt.Invoke(new[] { input });
        }

        protected DynamicInterfaceBase(object instance)
        {
            var instanceType = instance.GetType();

            _WritableProperty = new DynamicProperty(instanceType.GetProperty("WritableProperty"), instance);
            _ReadOnlyProperty = new DynamicProperty(instanceType.GetProperty("ReadOnlyProperty"), instance);

            _GetStringValue = new DynamicMethod(instanceType.GetMethod("GetStringValue"), instance);
            _GetInt = new DynamicMethod(instanceType.GetMethod("GetInt"), instance);

            _indexer = new DynamicIndex(instance);
        }


    }

    //public interface IDummy
    //{
    //    string WritableProperty { get; set; }

    //    string ReadOnlyProperty { get; }

    //    //string GetStringValue(object input);
    //}
}
