// MODULE SAY
const {CommandInfo, Message} = require("../../discord_interface");

class Module {
    /**
     * @param create_infos contains module infos => {
     *      client: discord_client
     * }
     */
    constructor(create_infos) {
        this.enabled = true // default value is true

        this.client = create_infos.client

        // Command declaration
        this.commands = [
            new CommandInfo('say', 'Fait parler bidibip')
                .add_text_option('message', 'Ce que bidibip dira pour vous', [], true)
                .set_member_only()
        ]
    }

    // When module is started
    start() {
    }

    // When module is stopped
    stop() {
    }

    // When server command is executed
    server_command(command) {
        if (command.match('say')) {
            this.client.say(
                new Message()
                    .set_channel(command.channel)
                    .set_text(command.option_value('message'))
            )
            command.skip()
        }
    }

    // On received server messages=
    server_message(message) {
    }

    // When interaction button is clicked (interaction should have been bound before)
    receive_interaction(value, id, message) {
    }

    // On update message on server
    server_message_updated(old_message, new_message) {
    }

    // On delete message on server
    server_message_delete(message) {
    }
}

module.exports = {Module}