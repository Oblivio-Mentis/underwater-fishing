// Development capture only. Timestamps enclose the real URP camera on the D3D11 device.
#include <atomic>
#include <cstdint>
#include <d3d11.h>
#include "IUnityGraphics.h"
#include "IUnityGraphicsD3D11.h"

namespace
{
    constexpr unsigned Capacity = 24;
    constexpr int BeginCamera = 41;
    constexpr int EndCamera = 42;

    struct QuerySlot
    {
        ID3D11Query* frequency = nullptr;
        ID3D11Query* first = nullptr;
        ID3D11Query* last = nullptr;
        bool pending = false;
    };

    IUnityGraphics* Graphics = nullptr;
    IUnityInterfaces* Interfaces = nullptr;
    ID3D11DeviceContext* Context = nullptr;
    QuerySlot Slots[Capacity];
    int Current = -1;
    std::atomic<uint64_t> ElapsedNanoseconds{0};
    std::atomic<uint64_t> Sample{0};
    std::atomic<int> DeviceReady{0};

    void ReleaseQueries()
    {
        DeviceReady.store(0);
        for (auto& slot : Slots)
        {
            if (slot.frequency) slot.frequency->Release();
            if (slot.first) slot.first->Release();
            if (slot.last) slot.last->Release();
            slot = {};
        }
        if (Context) Context->Release();
        Context = nullptr;
        Current = -1;
    }

    bool MakeQuery(ID3D11Device* device, D3D11_QUERY kind, ID3D11Query** query)
    {
        const D3D11_QUERY_DESC description{kind, 0};
        return SUCCEEDED(device->CreateQuery(&description, query));
    }

    void UNITY_INTERFACE_API OnDeviceEvent(UnityGfxDeviceEventType type)
    {
        if (type == kUnityGfxDeviceEventShutdown)
        {
            ReleaseQueries();
            return;
        }
        if (type != kUnityGfxDeviceEventInitialize || Context ||
            Graphics->GetRenderer() != kUnityGfxRendererD3D11) return;

        auto* graphicsD3D11 = Interfaces->Get<IUnityGraphicsD3D11>();
        auto* device = graphicsD3D11 ? graphicsD3D11->GetDevice() : nullptr;
        if (!device) return;
        device->GetImmediateContext(&Context);

        for (auto& slot : Slots)
        {
            if (MakeQuery(device, D3D11_QUERY_TIMESTAMP_DISJOINT, &slot.frequency)
                && MakeQuery(device, D3D11_QUERY_TIMESTAMP, &slot.first)
                && MakeQuery(device, D3D11_QUERY_TIMESTAMP, &slot.last)) continue;
            ReleaseQueries();
            return;
        }
        DeviceReady.store(1);
    }

    void Collect()
    {
        for (auto& slot : Slots)
        {
            if (!slot.pending) continue;
            D3D11_QUERY_DATA_TIMESTAMP_DISJOINT timer{};
            uint64_t start = 0, end = 0;
            constexpr auto flags = D3D11_ASYNC_GETDATA_DONOTFLUSH;
            if (Context->GetData(slot.frequency, &timer, sizeof timer, flags) != S_OK ||
                Context->GetData(slot.first, &start, sizeof start, flags) != S_OK ||
                Context->GetData(slot.last, &end, sizeof end, flags) != S_OK) continue;
            slot.pending = false;
            if (timer.Disjoint || end <= start || !timer.Frequency) continue;
            ElapsedNanoseconds.store(static_cast<uint64_t>(
                static_cast<double>(end-start) * 1.e9 / static_cast<double>(timer.Frequency)));
            Sample.fetch_add(1);
        }
    }

    void UNITY_INTERFACE_API OnCameraEvent(int event)
    {
        if (!Context) return;
        if (event == BeginCamera)
        {
            Collect();
            if (Current >= 0) return;
            for (unsigned i = 0; i < Capacity; ++i)
            {
                auto& slot = Slots[i];
                if (slot.pending) continue;
                Context->Begin(slot.frequency);
                Context->End(slot.first);
                Current = static_cast<int>(i);
                break;
            }
        }
        else if (event == EndCamera && Current >= 0)
        {
            auto& slot = Slots[Current];
            Context->End(slot.last);
            Context->End(slot.frequency);
            slot.pending = true;
            Current = -1;
        }
    }
}

extern "C"
{
    UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API UnityPluginLoad(IUnityInterfaces* interfaces)
    {
        Interfaces = interfaces;
        Graphics = interfaces->Get<IUnityGraphics>();
        Graphics->RegisterDeviceEventCallback(OnDeviceEvent);
        OnDeviceEvent(kUnityGfxDeviceEventInitialize);
    }

    UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API UnityPluginUnload()
    {
        if (Graphics) Graphics->UnregisterDeviceEventCallback(OnDeviceEvent);
        ReleaseQueries();
        Interfaces = nullptr;
        Graphics = nullptr;
    }

    UNITY_INTERFACE_EXPORT UnityRenderingEvent UNITY_INTERFACE_API GetArtTimingEvent()
    {
        return OnCameraEvent;
    }

    UNITY_INTERFACE_EXPORT int UNITY_INTERFACE_API ArtTimingReady()
    {
        return DeviceReady.load();
    }

    UNITY_INTERFACE_EXPORT uint64_t UNITY_INTERFACE_API ReadArtTiming(uint64_t* nanoseconds)
    {
        const auto sample = Sample.load();
        *nanoseconds = ElapsedNanoseconds.load();
        return sample;
    }
}
