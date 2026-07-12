#pragma once

#include <windows.h>

#define VDISPLAY_MAX_VM 4
#define VDISPLAY_MAX_FRAME_W 1920
#define VDISPLAY_MAX_FRAME_H 1080
#define VDISPLAY_FRAME_PITCH (VDISPLAY_MAX_FRAME_W * 4)
#define VDISPLAY_FRAME_SIZE (VDISPLAY_FRAME_PITCH * VDISPLAY_MAX_FRAME_H)

#define VDISPLAY_LAYOUT_MAP_NAME L"Global\\VDisplay.Layout"
#define VDISPLAY_FRAMES_MAP_NAME L"Global\\VDisplay.Frames"
#define VDISPLAY_MODES_CFG_PATH L"C:\\ProgramData\\VDisplay\\modes.cfg"
#define VDISPLAY_MAX_USER_MODES 16

struct VDisplayUserMode
{
    DWORD Width;
    DWORD Height;
    DWORD VSync;
};

#pragma pack(push, 1)

struct VDisplayVirtualRegion
{
    UINT SrcX;
    UINT SrcY;
    UINT SrcW;
    UINT SrcH;
    UINT DstW;
    UINT DstH;
    volatile LONG FrameReady;
};

struct VDisplaySharedLayout
{
    volatile LONG Version;
    volatile LONG CaptureActive;
    UINT SourceWidth;
    UINT SourceHeight;
    UINT MonitorCount;
    VDisplayVirtualRegion Vm[VDISPLAY_MAX_VM];
};

struct VDisplaySharedFrames
{
    BYTE Pixels[VDISPLAY_MAX_VM][VDISPLAY_FRAME_SIZE];
};

#pragma pack(pop)
