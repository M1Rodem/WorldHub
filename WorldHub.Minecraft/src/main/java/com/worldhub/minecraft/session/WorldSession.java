package com.worldhub.minecraft.session;

import java.nio.file.Path;
import java.time.Instant;
import java.util.UUID;

public final class WorldSession {

    private final UUID sessionId;
    private final String worldName;
    private final Path worldPath;
    private final Instant startedAt;

    private Instant endedAt;

    public WorldSession(
            UUID sessionId,
            String worldName,
            Path worldPath,
            Instant startedAt) {

        this.sessionId = sessionId;
        this.worldName = worldName;
        this.worldPath = worldPath;
        this.startedAt = startedAt;
    }

    public UUID getSessionId() {
        return sessionId;
    }

    public String getWorldName() {
        return worldName;
    }

    public Path getWorldPath() {
        return worldPath;
    }

    public Instant getStartedAt() {
        return startedAt;
    }

    public Instant getEndedAt() {
        return endedAt;
    }

    public boolean isActive() {
        return endedAt == null;
    }

    public void end() {
        if (endedAt == null) {
            endedAt = Instant.now();
        }
    }
}