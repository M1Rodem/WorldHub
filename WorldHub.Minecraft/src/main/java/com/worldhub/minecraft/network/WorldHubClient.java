package com.worldhub.minecraft.network;

import com.worldhub.minecraft.session.WorldSession;

import java.io.DataOutputStream;
import java.io.IOException;
import java.net.Socket;
import java.nio.charset.StandardCharsets;

public final class WorldHubClient {

    private static final String HOST = "127.0.0.1";
    private static final int PORT = 27071;

    private WorldHubClient() {
    }

    public static void sendSessionEnded(WorldSession session) {
        String message =
                "SESSION_ENDED|"
                        + session.getSessionId()
                        + "|"
                        + session.getWorldName()
                        + "|"
                        + session.getWorldPath();

        try (Socket socket = new Socket(HOST, PORT);
             DataOutputStream output =
                     new DataOutputStream(socket.getOutputStream())) {

            byte[] data = message.getBytes(StandardCharsets.UTF_8);

            output.writeInt(data.length);
            output.write(data);
            output.flush();

            System.out.println(
                    "[WorldHub] SESSION_ENDED sent: "
                            + session.getSessionId()
            );

        } catch (IOException exception) {
            System.err.println(
                    "[WorldHub] Failed to send SESSION_ENDED: "
                            + exception.getMessage()
            );
        }
    }
}