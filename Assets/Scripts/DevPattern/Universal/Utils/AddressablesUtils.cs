namespace DevPattern.Universal.Utils {
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public static class AddressablesUtils
{
    public static bool HasResult<T>(AsyncOperationHandle<T> handle) {
        return handle.IsValid() && handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null;
    }
}

}
