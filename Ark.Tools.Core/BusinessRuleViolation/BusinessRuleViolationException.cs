using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Ark.Tools.Core.BusinessRuleViolation
{
    [Serializable]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194:Implement exception constructors.", Justification = "Created from BusinessRuleViolation")]
    public sealed class BusinessRuleViolationException : InvalidOperationException
    {
        [NonSerialized]
        private BusinessRuleViolationState _businessRuleViolationState = new();

        public BusinessRuleViolationException(BusinessRuleViolation br) : base(br.Detail)
        {
            BusinessRuleViolation = br;
            _handleSerialization();
        }

        private void _handleSerialization()
        {
            SerializeObjectState += (object exception, SafeSerializationEventArgs eventArgs) => eventArgs.AddSerializedState(_businessRuleViolationState);
        }

        public BusinessRuleViolationException(BusinessRuleViolation br, Exception innerException) : base(br.Detail, innerException)
        {
        }

        public BusinessRuleViolation BusinessRuleViolation
        {
            get { return _businessRuleViolationState.BusinessRuleViolation; }
            set { _businessRuleViolationState.BusinessRuleViolation = value; }
        }

        // Implement the ISafeSerializationData interface
        // to contain custom  exception data in a partially trusted
        // assembly. Use this interface to replace the
        // Exception.GetObjectData method,
        // which is now marked with the SecurityCriticalAttribute.
        [Serializable]
        private struct BusinessRuleViolationState : ISafeSerializationData
        {
            private BusinessRuleViolation _businessRuleViolation;

            public BusinessRuleViolation BusinessRuleViolation
            {
                get { return _businessRuleViolation; }
                set { _businessRuleViolation = value; }
            }

            // This method is called when deserialization of the
            // exception is complete.
            void ISafeSerializationData.CompleteDeserialization
                (object obj)
            {
                // Since the exception simply contains an instance of
                // the exception state object, we can repopulate it
                // here by just setting its instance field to be equal
                // to this deserialized state instance.
                BusinessRuleViolationException exception = obj as BusinessRuleViolationException;
                exception._handleSerialization();
                exception._businessRuleViolationState = this;
            }
        }
    }


}
