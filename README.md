# Blip bloup !

<img src="https://raw.githubusercontent.com/Unreal-Engine-FR/.github/main/resources/bidibip-icon/bidibip_close.png"  width="200" height="200">

**Welcome to Bidibip's developers repository !**

## Deploy

### Create and run

- Install docker on your server
- Clone this repository (It should be cloned in a directory named `/home/$USER/docker/`)
- Configure .env file (rename `./Bidibip/.env.template` to `./Bidibip/.env` and fill it with your [credentials](https://www.writebots.com/discord-bot-token))
- Create docker image : `> source ./Bidibip/scripts/build-docker-image.sh`
- Create a docker volume named `bidibip-saved` [https://docs.docker.com/storage/volumes/](https://docs.docker.com/storage/volumes/)
- Run docker container : `> source ./Bidibip/scripts/create-docker-container.sh`

### Stop the server

- Find the container id with `> sudo docker ps -a`
- stop the container with `> sudo docker stop <container_id>`


## Development

### Setup local repository

- clone this repository
- install node dependencies `> npm install`
- Configure .env file (rename `./Bidibip/.env.template` to `./Bidibip/.env` and fill it with your [credentials](https://www.writebots.com/discord-bot-token))
- switch to dev branch
- setup git flow `> git flow init` (main branch:`main` / development branch:`dev` / Default configuration)
- create a new feature `> git flow feature start 'your-feature-name'`

### Run local instance

- `> npm start`

## Adding modules

> Features are implemented in modules. [See some examples](./src/modules/)

> To create a new module, add a new folder with your module's name under [src/modules](./src/modules/) directory, then add a module.js file that will contains all your module events.
>
> *Minimal module code example : `src/modules/my_feature/module.js`*
> ```js
> // MODULE MY_FEATURE
> class Module {
>     constructor(create_infos) {
>     }
> }
> 
> module.exports = {Module}
> ``` 

> Then, to receive events, implement corresponding method in your class.
> Here are some examples
> ```js
> class Module {
>    constructor(create_infos) {
>        this.client = create_infos.client
>
>        // Add a dummy command
>        this.commands = [
>            new CommandInfo('my_dummy_command', 'what my command do').set_member_only()
>        ]
>    }
>
>    async start() {}                                              // When module is started
>    async stop() {}                                               // When module is stopped
>    async server_interaction(command) {}                              // When server command is executed
>    async server_message(message) {}                              // On received server messages=
>    async receive_interaction(value, id, message) {}              // When interaction button is clicked (interaction should have been bound before)
>    async server_message_updated(old_message, new_message) {}     // On update message on server
>    async server_message_delete(message) {}                       // On delete message on server
> }
> ```
> *a more complete list of available events is available in [src/modules/utilities/module.js](./src/modules/utilities/module.js)*

That's all