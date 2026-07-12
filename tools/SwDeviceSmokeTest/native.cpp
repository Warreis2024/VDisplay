#include <windows.h>
#include <swdevice.h>
#include <stdio.h>

VOID WINAPI CreationCallback(
    _In_ HSWDEVICE hSwDevice,
    _In_ HRESULT hrCreateResult,
    _In_opt_ PVOID pContext,
    _In_opt_ PCWSTR pszDeviceInstanceId)
{
    HANDLE hEvent = *(HANDLE*)pContext;
    printf("Callback hr=0x%lx instance=%ls\n", hrCreateResult, pszDeviceInstanceId ? pszDeviceInstanceId : L"(null)");
    SetEvent(hEvent);
    UNREFERENCED_PARAMETER(hSwDevice);
}

int main()
{
    HANDLE hEvent = CreateEvent(nullptr, FALSE, FALSE, nullptr);
    HSWDEVICE hSwDevice = nullptr;
    SW_DEVICE_CREATE_INFO createInfo = { 0 };

    PCWSTR instanceId = L"VDisplayDriver";
    PCWSTR hardwareIds = L"VDisplayDriver\0\0";
    PCWSTR compatibleIds = L"VDisplayDriver\0\0";

    createInfo.cbSize = sizeof(createInfo);
    createInfo.pszzCompatibleIds = compatibleIds;
    createInfo.pszInstanceId = instanceId;
    createInfo.pszzHardwareIds = hardwareIds;
    createInfo.pszDeviceDescription = L"VDisplay test";
    createInfo.CapabilityFlags = SWDeviceCapabilitiesRemovable |
        SWDeviceCapabilitiesSilentInstall |
        SWDeviceCapabilitiesDriverRequired;

    HRESULT hr = SwDeviceCreate(
        L"VDisplayDriver",
        L"HTREE\\ROOT\\0",
        &createInfo,
        0,
        nullptr,
        CreationCallback,
        &hEvent,
        &hSwDevice);

    printf("SwDeviceCreate hr=0x%lx cbSize=%lu\n", hr, createInfo.cbSize);

    if (SUCCEEDED(hr))
    {
        WaitForSingleObject(hEvent, 15000);
        SwDeviceClose(hSwDevice);
    }

    CloseHandle(hEvent);
    return 0;
}
