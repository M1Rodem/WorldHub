package com.worldhub.minecraft.events;

import com.worldhub.minecraft.session.WorldSession;
import net.minecraft.client.Minecraft;
import net.minecraft.client.multiplayer.ClientLevel;
import net.minecraft.world.level.storage.LevelResource;
import net.minecraftforge.api.distmarker.Dist;
import net.minecraftforge.event.TickEvent;
import net.minecraftforge.eventbus.api.SubscribeEvent;
import net.minecraftforge.fml.common.Mod;
import com.worldhub.minecraft.connection.WorldHubConnection;
import com.worldhub.minecraft.network.WorldHubClient;

import java.nio.file.Path;
import java.time.Instant;
import java.util.UUID;

@Mod.EventBusSubscriber(
        modid = "worldhub",
        bus = Mod.EventBusSubscriber.Bus.FORGE,
        value = Dist.CLIENT
)
public final class WorldHubForgeEvents {

    private static WorldSession currentSession;

    private WorldHubForgeEvents() {
    }

    @SubscribeEvent
    public static void onClientTick(TickEvent.ClientTickEvent event) {
        if (event.phase != TickEvent.Phase.END) {
            return;
        }

        Minecraft minecraft = Minecraft.getInstance();
        ClientLevel level = minecraft.level;

        if (level != null && currentSession == null) {
            startSession(level);
            return;
        }

        if (level == null && currentSession != null) {
            endSession();
        }
    }

    private static void startSession(ClientLevel level) {
        WorldHubConnection.testConnection();
        
        String worldName = resolveWorldName();
        Path worldPath = resolveWorldPath();

        currentSession = new WorldSession(
                UUID.randomUUID(),
                worldName,
                worldPath,
                Instant.now()
        );

        System.out.println(
                "[WorldHub] Session started: "
                        + currentSession.getSessionId()
                        + ", world: "
                        + currentSession.getWorldName()
                        + ", path: "
                        + currentSession.getWorldPath()
        );
    }

    private static void endSession() {
        currentSession.end();

        System.out.println(
                "[WorldHub] Session ended: "
                        + currentSession.getSessionId()
                        + ", world: "
                        + currentSession.getWorldName()
        );

        WorldHubClient.sendSessionEnded(currentSession);

        currentSession = null;
    }

    private static String resolveWorldName() {
        Minecraft minecraft = Minecraft.getInstance();

        if (minecraft.hasSingleplayerServer()) {
            return minecraft.getSingleplayerServer()
                    .getWorldData()
                    .getLevelName();
        }

        return "Multiplayer";
    }

    private static Path resolveWorldPath() {
        Minecraft minecraft = Minecraft.getInstance();

        if (!minecraft.hasSingleplayerServer()) {
            return null;
        }

        return minecraft.getSingleplayerServer()
                .getWorldPath(LevelResource.ROOT)
                .toAbsolutePath()
                .normalize();
    }
}