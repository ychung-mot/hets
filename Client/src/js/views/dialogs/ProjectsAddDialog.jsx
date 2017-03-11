import React from 'react';

import { connect } from 'react-redux';

import { Form, FormGroup, HelpBlock, ControlLabel } from 'react-bootstrap';

import _ from 'lodash';

import * as Constant from '../../constants';

import EditDialog from '../../components/EditDialog.jsx';
import FilterDropdown from '../../components/FilterDropdown.jsx';
import FormInputControl from '../../components/FormInputControl.jsx';

import { isBlank, notBlank } from '../../utils/string';

var ProjectsAddDialog = React.createClass({
  propTypes: {
    currentUser: React.PropTypes.object,
    localAreas: React.PropTypes.object,
    onSave: React.PropTypes.func.isRequired,
    onClose: React.PropTypes.func.isRequired,
    show: React.PropTypes.bool,
  },

  getInitialState() {
    // Local Area (default to the first local area of the District of the logged in User, but allow any local area to be selected)
    var currentUser = this.props.currentUser;
    var localAreas = this.props.localAreas;
    var defaultLocalAreaId = _.find(localAreas, (x) => x.serviceArea.district.id === currentUser.district.id);
    
    return {
      name: '',
      provincialProjectNumber: '',
      localAreaId: defaultLocalAreaId || 0,
      information: '',
      nameError: '',
      provincialProjectNumberError: '',
      localAreaError: '',
    };
  },

  componentDidMount() {
    this.input.focus();
  },

  updateState(state, callback) {
    this.setState(state, callback);
  },

  didChange() {
    return notBlank(this.state.name) ||
      notBlank(this.state.provincialProjectNumber) ||
      notBlank(this.state.information) ||
      this.state.localAreaId !== 0;
  },

  isValid() {
    // Clear out any previous errors
    var valid = true;

    this.setState({
      nameError: '',
      provincialProjectNumberError: '',
      localAreaError: '',
    });

    if (isBlank(this.state.name)) {
      this.setState({ nameError: 'Name is required' });
      valid = false;
    }

    if (isBlank(this.state.provincialProjectNumber)) {
      this.setState({ provincialProjectNumberError: 'Provincial project number is required' });
      valid = false;
    }

    if (this.state.localAreaId === 0) {
      this.setState({ localAreaError: 'Local area is required' });
      valid = false;
    }

    return valid;
  },

  onSave() {
    this.props.onSave({
      name: this.state.name,
      provincialProjectNumber: this.state.provincialProjectNumber,
      localArea: { id: this.state.localAreaId }, 
      information: this.state.information,
      status: Constant.PROJECT_STATUS_CODE_ACTIVE,
    });
  },

  render() {
    var localAreas = _.sortBy(this.props.localAreas, 'name');

    return <EditDialog id="add-project" show={ this.props.show } bsSize="small"
      onClose={ this.props.onClose } onSave={ this.onSave } didChange={ this.didChange } isValid={ this.isValid }
      title= {
        <strong>Add Project</strong>
      }>
      <Form>
        <FormGroup controlId="name" validationState={ this.state.nameError ? 'error' : null }>
          <ControlLabel>Project Name <sup>*</sup></ControlLabel>
          <FormInputControl type="text" value={ this.state.name } updateState={ this.updateState } inputRef={ ref => { this.input = ref; }} />
          <HelpBlock>{ this.state.nameError }</HelpBlock>
        </FormGroup>
        <FormGroup controlId="provincialProjectNumber" validationState={ this.state.provincialProjectNumberError ? 'error' : null }>
          <ControlLabel>Provincial Project Number <sup>*</sup></ControlLabel>
          <FormInputControl type="text" value={ this.state.provincialProjectNumber } updateState={ this.updateState } />
          <HelpBlock>{ this.state.provincialProjectNumberError }</HelpBlock>
        </FormGroup>
        <FormGroup controlId="localAreaId" validationState={ this.state.localAreaError ? 'error' : null }>
          <ControlLabel>Local Area <sup>*</sup></ControlLabel>
          <FilterDropdown id="localAreaId" placeholder="None" blankLine="(None)"
            items={ localAreas } selectedId={ this.state.localAreaId } updateState={ this.updateState } />
          <HelpBlock>{ this.state.localAreaError }</HelpBlock>
        </FormGroup>
        <FormGroup controlId="information">
          <ControlLabel>Information</ControlLabel>
          <FormInputControl type="text" value={ this.state.information } updateState={ this.updateState } />
        </FormGroup>
      </Form>
    </EditDialog>;
  },
});

function mapStateToProps(state) {
  return {
    currentUser: state.user,
    localAreas: state.lookups.localAreas,
  };
}

export default connect(mapStateToProps)(ProjectsAddDialog);