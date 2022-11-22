FROM node:alpine AS BUILD_IMAGE
WORKDIR /usr/src/app
# Install app dependencies
# A wildcard is used to ensure both package.json AND package-lock.json are copied
# where available (npm@5+)

COPY . .

# If you are building your code for production
RUN npm install --production
RUN npm run build
#COPY ["package.json", "package-lock.json*", "npm-shrinkwrap.json*", "/src/"]
#RUN npm install --production && mv node_modules ../
#RUN npm install parcel --global
# Bundle app source
#COPY . .

FROM node:alpine
WORKDIR /usr/src/app

# copy from build image
#COPY --from=BUILD_IMAGE /usr/src/app/package*.json ./
COPY --from=BUILD_IMAGE /usr/src/app/dist ./dist
COPY --from=BUILD_IMAGE /usr/src/app/server.js .
RUN npm install express

EXPOSE 3000
RUN chown -R node /usr/src/app
USER node
CMD ["node" ,"server.js"]
