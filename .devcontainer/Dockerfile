# .NET dev container image. The mcr dotnet/sdk image is Ubuntu-based and ships a
# non-root 'ubuntu' (uid 1000) user. Pin the tag to the .NET version you target.
FROM mcr.microsoft.com/dotnet/sdk:10.0

# openssh-client: ssh + ssh-keyscan for agent-forwarded git.
RUN apt-get update \
  && apt-get install -y --no-install-recommends openssh-client \
  && rm -rf /var/lib/apt/lists/*

# Baseline SSH host keys so agent-forwarded git verifies these hosts with no
# per-developer known_hosts file. '|| true' tolerates a transient network failure.
RUN ssh-keyscan -t rsa,ecdsa,ed25519 github.com ssh.dev.azure.com >> /etc/ssh/ssh_known_hosts 2>/dev/null || true

