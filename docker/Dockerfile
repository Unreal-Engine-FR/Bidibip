FROM alpine:latest

RUN apk add --no-cache wget libstdc++ libgcc icu-libs

COPY entrypoint.sh /opt/bidibip/
RUN chmod +x /opt/bidibip/entrypoint.sh

WORKDIR /opt/bidibip

ENTRYPOINT ["/opt/bidibip/entrypoint.sh"]
