using System;
using Core.Results;
using OneOf;
using UnityEngine.InputSystem;
using Success = OneOf.Types.Success;

namespace Core.Input
{
    public static class InputBindingOverrides
    {
        public static OneOf<Success, Corrupted> Load(IInputActionCollection2 actions, string json)
        {
            try
            {
                actions.LoadBindingOverridesFromJson(json);
                return new Success();
            }
            catch (Exception exception) when (exception is ArgumentException or NullReferenceException)
            {
                actions.RemoveAllBindingOverrides();
                return new Corrupted($"Binding overrides are not valid JSON: {exception.Message}");
            }
        }
    }
}
