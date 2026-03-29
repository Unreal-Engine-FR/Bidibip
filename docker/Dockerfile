FROM alpine:latest

RUN apk add --no-cache wget bash ca-certificates libstdc++ libgcc icu-libs

# Install curl-impersonate (Chrome variant) to bypass Cloudflare TLS fingerprinting
RUN wget -O /tmp/curl-impersonate.tar.gz \
    https://github.com/lexiforest/curl-impersonate/releases/download/v1.5.1/curl-impersonate-v1.5.1.x86_64-linux-musl.tar.gz \
    && tar xzf /tmp/curl-impersonate.tar.gz -C /usr/local/bin/ \
    && rm /tmp/curl-impersonate.tar.gz

COPY entrypoint.sh /opt/bidibip/
RUN chmod +x /opt/bidibip/entrypoint.sh

WORKDIR /opt/bidibip

ENTRYPOINT ["/opt/bidibip/entrypoint.sh"]
