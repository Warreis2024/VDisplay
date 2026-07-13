#include <windows.h>
#include <swdevice.h>

static HSWDEVICE g_device = nullptr;
static HANDLE g_event = nullptr;
static HRESULT g_createResult = S_OK;

VOID WINAPI VDisplayCreationCallback(
    _In_ HSWDEVICE hSwDevice,
    _In_ HRESULT hrCreateResult,
    _In_opt_ PVOID pContext,
    _In_opt_ PCWSTR pszDeviceInstanceId)
{
    UNREFERENCED_PARAMETER(hSwDevice);
    UNREFERENCED_PARAMETER(pszDeviceInstanceId);
    g_createResult = hrCreateResult;
    if (pContext != nullptr)
    {
        SetEvent(*(HANDLE*)pContext);
    }
}

extern "C" __declspec(dllexport) long WINAPI VDisplayStartSoftwareDevice()
{
    if (g_device != nullptr)
    {
        return S_OK;
    }

    g_event = CreateEvent(nullptr, FALSE, FALSE, nullptr);
    if (g_event == nullptr)
    {
        return HRESULT_FROM_WIN32(GetLastError());
    }

    SW_DEVICE_CREATE_INFO createInfo = { 0 };
    // Match INF Models: Root\VDisplayDriver and VDisplayDriver (IddSample pattern)
    PCWSTR instanceId = L"VDisplayDriver";
    PCWSTR hardwareIds = L"Root\\VDisplayDriver\0VDisplayDriver\0\0";
    PCWSTR compatibleIds = L"Root\\VDisplayDriver\0VDisplayDriver\0\0";

    createInfo.cbSize = sizeof(createInfo);
    createInfo.pszInstanceId = instanceId;
    createInfo.pszzHardwareIds = hardwareIds;
    createInfo.pszzCompatibleIds = compatibleIds;
    createInfo.pszDeviceDescription = L"VDisplay Virtual Split Monitor Driver";
    createInfo.CapabilityFlags =
        SWDeviceCapabilitiesRemovable |
        SWDeviceCapabilitiesSilentInstall |
        SWDeviceCapabilitiesDriverRequired;

    HRESULT hr = SwDeviceCreate(
        L"VDisplayDriver",
        L"HTREE\\ROOT\\0",
        &createInfo,
        0,
        nullptr,
        VDisplayCreationCallback,
        &g_event,
        &g_device);

    if (FAILED(hr))
    {
        CloseHandle(g_event);
        g_event = nullptr;
        return hr;
    }

    DWORD waitResult = WaitForSingleObject(g_event, 15000);
    if (waitResult != WAIT_OBJECT_0)
    {
        SwDeviceClose(g_device);
        g_device = nullptr;
        CloseHandle(g_event);
        g_event = nullptr;
        return HRESULT_FROM_WIN32(ERROR_TIMEOUT);
    }

    if (FAILED(g_createResult))
    {
        SwDeviceClose(g_device);
        g_device = nullptr;
        CloseHandle(g_event);
        g_event = nullptr;
        return g_createResult;
    }

    return S_OK;
}

extern "C" __declspec(dllexport) void WINAPI VDisplayStopSoftwareDevice()
{
    if (g_device != nullptr)
    {
        SwDeviceClose(g_device);
        g_device = nullptr;
    }

    if (g_event != nullptr)
    {
        CloseHandle(g_event);
        g_event = nullptr;
    }
}

extern "C" __declspec(dllexport) BOOL WINAPI VDisplayIsSoftwareDeviceRunning()
{
    return g_device != nullptr ? TRUE : FALSE;
}
