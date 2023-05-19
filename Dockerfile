FROM node:18-alpine
ENV NODE_ENV=production
WORKDIR /home/node/Bidibip

# Install app dependencies
# A wildcard is used to ensure both package.json AND package-lock.json are copied
# where available (npm@5+)
COPY package*.json ./

RUN npm install --production
RUN apk add git # required to auto-update app from git
# If you are building your code for production
RUN npm ci --omit=dev

# Bundle app source
COPY . .

CMD [ "node", "src/index.js" ]